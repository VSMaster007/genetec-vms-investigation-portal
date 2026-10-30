using Investigation.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Application;

public sealed class CameraCatalogService
{
    private readonly IInvestigationStore _store;
    private readonly IGenetecService _genetec;
    private readonly IOptions<GenetecModeOptions> _mode;
    private readonly ILogger<CameraCatalogService> _logger;

    public CameraCatalogService(
        IInvestigationStore store,
        IGenetecService genetec,
        IOptions<GenetecModeOptions> mode,
        ILogger<CameraCatalogService> logger)
    {
        _store = store;
        _genetec = genetec;
        _mode = mode;
        _logger = logger;
    }

    public bool IsRealMode => string.Equals(_mode.Value.Mode, "Real", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<CameraDto>> RefreshAsync(CancellationToken cancellationToken)
    {
        var discovered = await _genetec.GetCamerasAsync(cancellationToken);
        await _store.UpsertDiscoveredCamerasAsync(
            discovered.Select(camera => new DiscoveredCamera(
                camera.Guid,
                camera.Name,
                camera.Description,
                camera.IsOnline,
                camera.AccessGranted,
                camera.IsMock)).ToArray(),
            cancellationToken);

        _logger.LogInformation("Camera catalog refreshed with {Count} cameras from the {Mode} adapter.", discovered.Count, _mode.Value.Mode);
        return await ListAsync(null, includeMock: true, cancellationToken);
    }

    public async Task<IReadOnlyList<CameraDto>> ListAsync(string? search, bool includeMock, CancellationToken cancellationToken)
    {
        var cameras = await _store.ListCamerasAsync(search, includeMock, cancellationToken);
        return cameras.Select(ToDto).ToArray();
    }

    public async Task<OperationResult<CameraDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await _store.FindCameraAsync(id, cancellationToken);
        return camera is null
            ? OperationResult<CameraDto>.Fail("CameraNotFound", "The camera was not found.", 404)
            : OperationResult<CameraDto>.Ok(ToDto(camera));
    }

    public async Task<MappingConsistencyDto> GetMappingsAsync(CancellationToken cancellationToken)
    {
        var mappings = await _store.ListMappingsAsync(cancellationToken);
        var unmapped = await _store.ListUnmappedExternalIdsAsync(cancellationToken);
        var issues = new List<string>();
        foreach (var mapping in mappings)
        {
            if (mapping.Camera is null)
            {
                issues.Add($"{mapping.ExternalCameraId} points at a camera that is no longer in the catalog.");
                continue;
            }

            if (IsRealMode && mapping.Camera.IsMock)
            {
                issues.Add($"{mapping.ExternalCameraId} is mapped to mock camera {mapping.Camera.CameraName}. Real mode will not play it.");
            }
        }

        foreach (var externalId in unmapped)
        {
            issues.Add($"{externalId} appears on incident records and has no Genetec mapping.");
        }

        return new MappingConsistencyDto(mappings.Select(ToMappingDto).ToArray(), unmapped, issues);
    }

    public async Task<OperationResult<CameraMappingDto>> CreateMappingAsync(string externalCameraId, Guid cameraId, CancellationToken cancellationToken)
    {
        var validation = await ValidateMappingAsync(externalCameraId, cameraId, null, cancellationToken);
        if (!validation.Succeeded)
        {
            return OperationResult<CameraMappingDto>.Fail(validation.ErrorCode!, validation.ErrorMessage!, validation.StatusCode);
        }

        var valid = validation.Value;
        var mapping = await _store.AddMappingAsync(valid.ExternalId, valid.Camera.Id, cancellationToken);
        mapping.Camera = valid.Camera;
        return OperationResult<CameraMappingDto>.Ok(ToMappingDto(mapping));
    }

    public async Task<OperationResult<CameraMappingDto>> UpdateMappingAsync(Guid id, string externalCameraId, Guid cameraId, CancellationToken cancellationToken)
    {
        var validation = await ValidateMappingAsync(externalCameraId, cameraId, id, cancellationToken);
        if (!validation.Succeeded)
        {
            return OperationResult<CameraMappingDto>.Fail(validation.ErrorCode!, validation.ErrorMessage!, validation.StatusCode);
        }

        var valid = validation.Value;
        var mapping = await _store.UpdateMappingAsync(id, valid.ExternalId, valid.Camera.Id, cancellationToken);
        if (mapping is null)
        {
            return OperationResult<CameraMappingDto>.Fail("MappingNotFound", "The mapping was not found.", 404);
        }

        mapping.Camera = valid.Camera;
        return OperationResult<CameraMappingDto>.Ok(ToMappingDto(mapping));
    }

    public Task<bool> DeleteMappingAsync(Guid id, CancellationToken cancellationToken) =>
        _store.DeleteMappingAsync(id, cancellationToken);

    private async Task<OperationResult<(string ExternalId, Camera Camera)>> ValidateMappingAsync(
        string externalCameraId,
        Guid cameraId,
        Guid? currentMappingId,
        CancellationToken cancellationToken)
    {
        var externalId = externalCameraId.Trim();
        if (!ExternalCameraIdRules.IsValid(externalId))
        {
            return OperationResult<(string, Camera)>.Fail(
                "InvalidExternalCameraId",
                "External camera IDs must be 2-64 characters and use letters, numbers, dots, underscores, or hyphens.",
                400);
        }

        var camera = await _store.FindCameraAsync(cameraId, cancellationToken);
        if (camera is null)
        {
            return OperationResult<(string, Camera)>.Fail(
                "CameraNotFound",
                "Refresh the camera catalog and choose a camera that exists.",
                404);
        }

        if (IsRealMode && camera.IsMock)
        {
            return OperationResult<(string, Camera)>.Fail(
                "MockCameraRejected",
                "Mock cameras cannot be mapped while Genetec mode is Real.",
                400);
        }

        var existing = await _store.FindMappingByExternalIdAsync(externalId, cancellationToken);
        if (existing is not null && existing.Id != currentMappingId)
        {
            return OperationResult<(string, Camera)>.Fail(
                "DuplicateMapping",
                "That external camera ID is already mapped.",
                409);
        }

        return OperationResult<(string, Camera)>.Ok((externalId, camera));
    }

    private static CameraDto ToDto(Camera camera) => new(
        camera.Id,
        camera.GenetecCameraGuid,
        camera.CameraName,
        camera.Location,
        camera.Status.ToString(),
        camera.IsMock,
        camera.CreatedAt,
        camera.UpdatedAt);

    public static CameraMappingDto ToMappingDto(CameraMapping mapping)
    {
        var camera = mapping.Camera;
        return new CameraMappingDto(
            mapping.Id,
            mapping.ExternalCameraId,
            mapping.GenetecCameraId,
            camera?.GenetecCameraGuid ?? Guid.Empty,
            camera?.CameraName ?? string.Empty,
            camera?.Location ?? string.Empty,
            camera?.Status.ToString() ?? CameraStatus.Unknown.ToString(),
            camera?.IsMock ?? false,
            mapping.CreatedAt,
            mapping.UpdatedAt);
    }
}

public sealed class GenetecModeOptions
{
    public const string SectionName = "Genetec";
    public string Mode { get; set; } = "Mock";
    public string RealTransport { get; set; } = "WebSdk";
}
