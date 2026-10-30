using Investigation.Domain;

namespace Investigation.Application;

public sealed record OperationResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage, int StatusCode)
{
    public static OperationResult<T> Ok(T value) => new(true, value, null, null, 200);

    public static OperationResult<T> Fail(string code, string message, int statusCode) =>
        new(false, default, code, message, statusCode);
}

public sealed record NormalizedIncident(
    string ExternalIncidentId,
    string Title,
    string Description,
    string ExternalCameraId,
    DateTime EventTimestampUtc,
    string EventType,
    string Location);

public sealed record ExternalIncidentDto(
    string IncidentId,
    string Title,
    string Description,
    string CameraId,
    string Timestamp,
    string EventType,
    string Location);

public sealed record IncidentSearchCriteria(
    string? IncidentId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Camera,
    string? EventType,
    string? Location,
    int Page,
    int PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record IncidentListItemDto(
    Guid Id,
    string ExternalIncidentId,
    string Title,
    string Description,
    string EventType,
    string? CameraName,
    string ExternalCameraId,
    Guid? GenetecCameraGuid,
    string Location,
    DateTime EventTimestampUtc,
    string MappingStatus,
    string RecordingState,
    string? RecordingMessage,
    bool RecordingIsMock);

public sealed record DiscoveredCamera(
    Guid GenetecGuid,
    string Name,
    string? Description,
    bool? IsOnline,
    bool AccessGranted,
    bool IsMock);

public sealed record CameraDto(
    Guid Id,
    Guid GenetecCameraGuid,
    string CameraName,
    string Location,
    string Status,
    bool IsMock,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CameraMappingDto(
    Guid Id,
    string ExternalCameraId,
    Guid CameraId,
    Guid GenetecCameraGuid,
    string CameraName,
    string Location,
    string CameraStatus,
    bool IsMock,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record MappingConsistencyDto(
    IReadOnlyList<CameraMappingDto> Mappings,
    IReadOnlyList<string> UnmappedExternalIds,
    IReadOnlyList<string> Issues);

public sealed record AuditEntry(Guid? UserId, Guid? IncidentId, string Action, string Details);

public sealed record AuditDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    Guid? IncidentId,
    string? ExternalIncidentId,
    string Action,
    string Details,
    DateTime CreatedAt);

public sealed record AuthenticatedUser(Guid Id, string Email, string DisplayName, string Role);

public interface IPasswordHasher
{
    string Hash(ApplicationUser user, string password);
    bool Verify(ApplicationUser user, string password);
}

public interface IInvestigationStore
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    Task<int> CountUsersAsync(CancellationToken cancellationToken);
    Task<ApplicationUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task AddUserAsync(ApplicationUser user, CancellationToken cancellationToken);

    Task<Incident?> FindIncidentAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<Incident>> SearchIncidentsAsync(IncidentSearchCriteria criteria, CancellationToken cancellationToken);
    Task<int> UpsertIncidentsAsync(IReadOnlyList<NormalizedIncident> incidents, CancellationToken cancellationToken);
    Task<int> CountIncidentsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListUnmappedExternalIdsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListEventTypesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListLocationsAsync(CancellationToken cancellationToken);

    Task<Camera?> FindCameraAsync(Guid id, CancellationToken cancellationToken);
    Task<Camera?> FindCameraByGenetecGuidAsync(Guid genetecGuid, CancellationToken cancellationToken);
    Task<IReadOnlyList<Camera>> ListCamerasAsync(string? search, bool includeMock, CancellationToken cancellationToken);
    Task UpsertDiscoveredCamerasAsync(IReadOnlyList<DiscoveredCamera> cameras, CancellationToken cancellationToken);

    Task<IReadOnlyList<CameraMapping>> ListMappingsAsync(CancellationToken cancellationToken);
    Task<CameraMapping?> FindMappingAsync(Guid id, CancellationToken cancellationToken);
    Task<CameraMapping?> FindMappingByExternalIdAsync(string externalCameraId, CancellationToken cancellationToken);
    Task<CameraMapping> AddMappingAsync(string externalCameraId, Guid cameraId, CancellationToken cancellationToken);
    Task<CameraMapping?> UpdateMappingAsync(Guid id, string externalCameraId, Guid cameraId, CancellationToken cancellationToken);
    Task<bool> DeleteMappingAsync(Guid id, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditEntry entry, CancellationToken cancellationToken);
    Task<bool> HasRecentAuditAsync(Guid? userId, Guid? incidentId, string action, TimeSpan window, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvestigationAudit>> ListAuditAsync(Guid? userId, int take, CancellationToken cancellationToken);
    Task<int> CountRecentAuditAsync(Guid? userId, DateTime sinceUtc, CancellationToken cancellationToken);
}

public interface IIncidentSource
{
    string Mode { get; }
    Task<IReadOnlyList<ExternalIncidentDto>> GetIncidentsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<ComponentCheck> CheckHealthAsync(CancellationToken cancellationToken);
}

public sealed record ComponentCheck(string State, string Detail, bool Attempted);

public sealed record GenetecConnectionStatus(
    string Mode,
    string Transport,
    bool SdkAssembliesPresent,
    bool Attempted,
    bool DirectoryConnected,
    bool Authenticated,
    string CertificateStatus,
    bool MediaGatewayAttempted,
    bool MediaGatewayReachable,
    string? MediaGatewayDetail,
    string? DirectoryDetail,
    string? Error,
    DateTime CheckedAtUtc);

public sealed record GenetecCameraInfo(
    Guid Guid,
    string Name,
    string? Description,
    bool? IsOnline,
    bool AccessGranted,
    bool IsMock);

public sealed record RecordingQueryResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsMock,
    TimeSpan? Retention,
    IReadOnlyList<TimeRange> Sequences);

public sealed record PlaybackEndpointInfo(
    string Mode,
    string Provider,
    string? MediaGatewayEndpoint,
    string? GwpScriptUrl,
    bool SupportsBrowserPlayback,
    bool IsMock,
    string Notice);

public sealed record MediaGatewayTokenResult(bool Succeeded, string? Token, string? ErrorCode, string? ErrorMessage);

public sealed record GenetecPlaybackHandle(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage,
    PlaybackEndpointInfo Endpoint,
    RecordingQueryResult Recording);

public interface IGenetecService
{
    Task<GenetecConnectionStatus> ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<GenetecConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GenetecCameraInfo>> GetCamerasAsync(CancellationToken cancellationToken);
    Task<GenetecCameraInfo?> GetCameraByIdAsync(Guid cameraGuid, CancellationToken cancellationToken);
    Task<RecordingQueryResult> GetRecordingAvailabilityAsync(Guid cameraGuid, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken);
    Task<GenetecPlaybackHandle> ResolvePlaybackAsync(Guid cameraGuid, DateTime seekUtc, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken);
    Task<MediaGatewayTokenResult> GetPlaybackTokenAsync(Guid cameraGuid, CancellationToken cancellationToken);
    PlaybackEndpointInfo GetPlaybackEndpoint();
}

public sealed record PlaybackSessionDto(
    string Mode,
    string Provider,
    bool IsGenetecRecording,
    bool CanPlay,
    string Notice,
    Guid IncidentId,
    string ExternalIncidentId,
    string Title,
    string ExternalCameraId,
    string? CameraName,
    Guid? CameraId,
    Guid? GenetecCameraGuid,
    string? MediaGatewayEndpoint,
    string? GwpScriptUrl,
    string? TokenPath,
    DateTime SeekUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int PreEventSeconds,
    int PostEventSeconds,
    RecordingAvailabilityDto Availability);

public sealed record RecordingAvailabilityDto(
    string State,
    bool CoversEvent,
    bool IsMockArchive,
    string Message,
    IReadOnlyList<TimeRange> Sequences,
    IReadOnlyList<TimeRange> Gaps);

public sealed record DashboardDto(
    int TotalIncidents,
    int TodaysIncidents,
    int CatalogCameras,
    int OnlineCameras,
    int MappedCameras,
    string CameraCatalogMode,
    GenetecConnectionStatus Genetec,
    IReadOnlyList<IncidentListItemDto> RecentIncidents,
    IReadOnlyList<AuditDto> RecentInvestigations);

public sealed record SystemStatusDto(
    ComponentCheck Api,
    ComponentCheck Database,
    ComponentCheck GenetecDirectory,
    ComponentCheck SdkAuthentication,
    ComponentCheck MediaGateway,
    ComponentCheck ExternalApi,
    string GenetecMode,
    string Transport,
    bool SdkAssembliesPresent);

public sealed record SyncResultDto(int Imported, int Rejected, IReadOnlyList<string> Errors);

public sealed record LoginResultDto(string Token, DateTime ExpiresAtUtc, AuthenticatedUser User);
