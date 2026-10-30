using Investigation.Application;
using Microsoft.Extensions.Logging;

namespace Investigation.Genetec;

public sealed class PlatformSdkUnavailableService : IGenetecService
{
    private readonly TimeProvider _time;
    private readonly ILogger<PlatformSdkUnavailableService> _logger;

    public PlatformSdkUnavailableService(TimeProvider time, ILogger<PlatformSdkUnavailableService> logger)
    {
        _time = time;
        _logger = logger;
    }

    public Task<GenetecConnectionStatus> ConnectAsync(CancellationToken cancellationToken) => StatusAsync();

    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<GenetecConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken) => StatusAsync();

    public Task<IReadOnlyList<GenetecCameraInfo>> GetCamerasAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GenetecCameraInfo>>(Array.Empty<GenetecCameraInfo>());

    public Task<GenetecCameraInfo?> GetCameraByIdAsync(Guid cameraGuid, CancellationToken cancellationToken) =>
        Task.FromResult<GenetecCameraInfo?>(null);

    public Task<RecordingQueryResult> GetRecordingAvailabilityAsync(Guid cameraGuid, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken) =>
        Task.FromResult(new RecordingQueryResult(false, "SdkNotInstalled", Detail, false, null, Array.Empty<TimeRange>()));

    public Task<GenetecPlaybackHandle> ResolvePlaybackAsync(Guid cameraGuid, DateTime seekUtc, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken) =>
        Task.FromResult(new GenetecPlaybackHandle(false, "SdkNotInstalled", Detail, GetPlaybackEndpoint(), new RecordingQueryResult(false, "SdkNotInstalled", Detail, false, null, Array.Empty<TimeRange>())));

    public Task<MediaGatewayTokenResult> GetPlaybackTokenAsync(Guid cameraGuid, CancellationToken cancellationToken) =>
        Task.FromResult(new MediaGatewayTokenResult(false, null, "SdkNotInstalled", Detail));

    public PlaybackEndpointInfo GetPlaybackEndpoint() =>
        new("Real", "None", null, null, false, false, Detail);

    private Task<GenetecConnectionStatus> StatusAsync()
    {
        _logger.LogWarning("{Detail}", Detail);
        return Task.FromResult(new GenetecConnectionStatus(
            "Real",
            "Platform",
            false,
            true,
            false,
            false,
            "SdkNotInstalled",
            false,
            false,
            Detail,
            Detail,
            Detail,
            _time.GetUtcNow().UtcDateTime));
    }

    private const string Detail =
        "Genetec:RealTransport is Platform, but Genetec.Sdk.dll is not referenced. Install the Security Center SDK on Windows, set GSC_SDK_CORE, and build net8.0-windows. On this host use Genetec:RealTransport=WebSdk.";
}
