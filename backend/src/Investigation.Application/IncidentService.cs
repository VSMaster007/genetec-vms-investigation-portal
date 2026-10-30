using Investigation.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Application;

public sealed class IncidentService
{
    private readonly IInvestigationStore _store;
    private readonly IIncidentSource _source;
    private readonly IGenetecService _genetec;
    private readonly IMemoryCache _cache;
    private readonly PlaybackOptions _playback;
    private readonly PortalOptions _portal;
    private readonly TimeProvider _time;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(
        IInvestigationStore store,
        IIncidentSource source,
        IGenetecService genetec,
        IMemoryCache cache,
        IOptions<PlaybackOptions> playback,
        IOptions<PortalOptions> portal,
        TimeProvider time,
        ILogger<IncidentService> logger)
    {
        _store = store;
        _source = source;
        _genetec = genetec;
        _cache = cache;
        _playback = playback.Value;
        _portal = portal.Value;
        _time = time;
        _logger = logger;
    }

    public Task<ComponentCheck> CheckSourceHealthAsync(CancellationToken cancellationToken) =>
        _source.CheckHealthAsync(cancellationToken);

    public async Task<SyncResultDto> SyncAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var external = await _source.GetIncidentsAsync(fromUtc, toUtc, cancellationToken);
        var accepted = new List<NormalizedIncident>();
        var errors = new List<string>();

        foreach (var item in external)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.IncidentId) || string.IsNullOrWhiteSpace(item.CameraId))
                {
                    errors.Add("An incident was skipped because it has no incident id or camera id.");
                    continue;
                }

                accepted.Add(new NormalizedIncident(
                    item.IncidentId.Trim(),
                    item.Title?.Trim() ?? string.Empty,
                    item.Description?.Trim() ?? string.Empty,
                    item.CameraId.Trim(),
                    TimestampNormalizer.ParseUtc(item.Timestamp),
                    item.EventType?.Trim() ?? string.Empty,
                    item.Location?.Trim() ?? string.Empty));
            }
            catch (TimestampNormalizationException exception)
            {
                errors.Add($"{item.IncidentId}: {exception.Message}");
                _logger.LogWarning("Rejected incident {IncidentId}: {Reason}", item.IncidentId, exception.Message);
            }
        }

        var imported = await _store.UpsertIncidentsAsync(accepted, cancellationToken);
        return new SyncResultDto(imported, errors.Count, errors);
    }

    public async Task<PagedResult<IncidentListItemDto>> SearchAsync(IncidentSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var paging = PageRules.Normalize(criteria.Page, criteria.PageSize, _portal.MaxPageSize);
        var page = await _store.SearchIncidentsAsync(criteria with { Page = paging.Page, PageSize = paging.PageSize }, cancellationToken);
        var items = new List<IncidentListItemDto>(page.Items.Count);
        foreach (var incident in page.Items)
        {
            items.Add(await ToListItemAsync(incident, includeRecording: true, cancellationToken));
        }

        return new PagedResult<IncidentListItemDto>(items, page.Page, page.PageSize, page.Total);
    }

    public async Task<OperationResult<IncidentListItemDto>> GetAsync(Guid id, Guid? userId, bool recordSelection, CancellationToken cancellationToken)
    {
        var incident = await _store.FindIncidentAsync(id, cancellationToken);
        if (incident is null)
        {
            return OperationResult<IncidentListItemDto>.Fail("IncidentNotFound", "The incident was not found.", 404);
        }

        if (recordSelection)
        {
            await RecordSelectionAsync(userId, incident, cancellationToken);
        }

        return OperationResult<IncidentListItemDto>.Ok(await ToListItemAsync(incident, includeRecording: true, cancellationToken));
    }

    public async Task<IncidentListItemDto> ToListItemAsync(Incident incident, bool includeRecording, CancellationToken cancellationToken)
    {
        var mapping = await _store.FindMappingByExternalIdAsync(incident.ExternalCameraId, cancellationToken);
        var camera = mapping?.Camera;
        var mappingStatus = camera is null ? "Unmapped" : "Mapped";
        var recordingState = "NotChecked";
        string? recordingMessage = camera is null ? "No camera mapping." : null;
        var recordingIsMock = camera?.IsMock ?? false;

        if (includeRecording && camera is not null)
        {
            var assessment = await AssessAsync(camera, incident.EventTimestampUtc, cancellationToken);
            recordingState = assessment.State;
            recordingMessage = assessment.Message;
        }

        return new IncidentListItemDto(
            incident.Id,
            incident.ExternalIncidentId,
            incident.Title,
            incident.Description,
            incident.EventType,
            camera?.CameraName,
            incident.ExternalCameraId,
            camera?.GenetecCameraGuid,
            incident.Location,
            incident.EventTimestampUtc,
            mappingStatus,
            recordingState,
            recordingMessage,
            recordingIsMock);
    }

    private async Task RecordSelectionAsync(Guid? userId, Incident incident, CancellationToken cancellationToken)
    {
        if (await _store.HasRecentAuditAsync(userId, incident.Id, AuditActions.IncidentSelected, TimeSpan.FromSeconds(3), cancellationToken))
        {
            return;
        }

        await _store.AddAuditAsync(
            new AuditEntry(userId, incident.Id, AuditActions.IncidentSelected, $"Selected {incident.ExternalIncidentId}."),
            cancellationToken);
    }

    public async Task<RecordingAssessment> AssessAsync(Camera camera, DateTime eventTimestampUtc, CancellationToken cancellationToken)
    {
        PlaybackWindow window;
        try
        {
            window = PlaybackWindowCalculator.Create(
                eventTimestampUtc,
                _playback.DefaultPreEventSeconds,
                _playback.DefaultPostEventSeconds,
                _playback.MaxWindowSeconds);
        }
        catch (TimestampNormalizationException exception)
        {
            return new RecordingAssessment("InvalidTimestamp", false, false, exception.Message, Array.Empty<TimeRange>(), Array.Empty<TimeRange>());
        }

        var cacheKey = $"rec:{camera.GenetecCameraGuid:N}:{window.StartUtc:O}:{window.EndUtc:O}";
        if (_cache.TryGetValue(cacheKey, out RecordingAssessment? cached) && cached is not null)
        {
            return cached;
        }

        var cameraInfo = await _genetec.GetCameraByIdAsync(camera.GenetecCameraGuid, cancellationToken);
        var recording = cameraInfo is null
            ? new RecordingQueryResult(false, "CameraNotFound", "Camera was not returned by the integration.", camera.IsMock, null, Array.Empty<TimeRange>())
            : await _genetec.GetRecordingAvailabilityAsync(camera.GenetecCameraGuid, window.StartUtc, window.EndUtc, cancellationToken);

        var assessment = RecordingWindowAnalyzer.Assess(
            window.StartUtc,
            window.EventUtc,
            window.EndUtc,
            _time.GetUtcNow().UtcDateTime,
            recording.Retention,
            cameraInfo is not null,
            cameraInfo?.AccessGranted ?? false,
            recording.Succeeded,
            recording.ErrorCode,
            recording.Sequences);

        _cache.Set(cacheKey, assessment, TimeSpan.FromSeconds(30));
        return assessment;
    }
}
