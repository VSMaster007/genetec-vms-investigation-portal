using Investigation.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Genetec;

public sealed class MockGenetecService : IGenetecService
{
    private readonly GenetecOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MockGenetecService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GenetecConnectionStatus? _status;

    public MockGenetecService(IOptions<GenetecOptions> options, TimeProvider time, ILogger<MockGenetecService> logger)
    {
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public Task<GenetecConnectionStatus> ConnectAsync(CancellationToken cancellationToken) => ProbeAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _status = null;
        _logger.LogInformation("Mock Genetec adapter disconnected. No Directory session existed.");
        return Task.CompletedTask;
    }

    public Task<GenetecConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken) => ProbeAsync(cancellationToken);

    public async Task<IReadOnlyList<GenetecCameraInfo>> GetCamerasAsync(CancellationToken cancellationToken)
    {
        var status = await ProbeAsync(cancellationToken);
        if (_options.Mock.FailConnection || !string.IsNullOrWhiteSpace(status.Error))
        {
            return Array.Empty<GenetecCameraInfo>();
        }

        return MockCameraCatalog.Cameras.Select(ToInfo).ToArray();
    }

    public async Task<GenetecCameraInfo?> GetCameraByIdAsync(Guid cameraGuid, CancellationToken cancellationToken)
    {
        var status = await ProbeAsync(cancellationToken);
        if (_options.Mock.FailConnection || !string.IsNullOrWhiteSpace(status.Error))
        {
            return null;
        }

        var camera = MockCameraCatalog.Cameras.FirstOrDefault(candidate => candidate.Guid == cameraGuid);
        return camera is null ? null : ToInfo(camera);
    }

    public async Task<RecordingQueryResult> GetRecordingAvailabilityAsync(Guid cameraGuid, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var camera = MockCameraCatalog.Cameras.FirstOrDefault(candidate => candidate.Guid == cameraGuid);
        if (camera is null)
        {
            return new RecordingQueryResult(false, "CameraNotFound", "Mock catalog does not contain that camera.", true, null, Array.Empty<TimeRange>());
        }

        if (!camera.AccessGranted)
        {
            return new RecordingQueryResult(false, "InsufficientPermissions", "Mock camera is configured to deny playback.", true, camera.Retention, Array.Empty<TimeRange>());
        }

        await ProbeAsync(cancellationToken);
        return new RecordingQueryResult(true, null, null, true, camera.Retention, BuildSequences(camera, startUtc, endUtc));
    }

    public async Task<GenetecPlaybackHandle> ResolvePlaybackAsync(Guid cameraGuid, DateTime seekUtc, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var endpoint = GetPlaybackEndpoint();
        var camera = await GetCameraByIdAsync(cameraGuid, cancellationToken);
        if (camera is null)
        {
            return new GenetecPlaybackHandle(false, "CameraNotFound", "Mock catalog does not contain that camera.", endpoint, new RecordingQueryResult(false, "CameraNotFound", "Camera not found.", true, null, Array.Empty<TimeRange>()));
        }

        var recording = await GetRecordingAvailabilityAsync(cameraGuid, startUtc, endUtc, cancellationToken);
        return new GenetecPlaybackHandle(recording.Succeeded && camera.AccessGranted, recording.ErrorCode, recording.ErrorMessage, endpoint, recording);
    }

    public Task<MediaGatewayTokenResult> GetPlaybackTokenAsync(Guid cameraGuid, CancellationToken cancellationToken) =>
        Task.FromResult(new MediaGatewayTokenResult(
            false,
            null,
            "MockMode",
            "Mock mode does not issue Genetec Media Gateway tokens and does not play archived video."));

    public PlaybackEndpointInfo GetPlaybackEndpoint() => new(
        "Mock",
        "None",
        null,
        null,
        false,
        true,
        "Mock playback surface. The Genetec Web Player is not connected and this is not an archived recording.");

    private async Task<GenetecConnectionStatus> ProbeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_status is not null && _time.GetUtcNow() - _status.CheckedAtUtc < TimeSpan.FromSeconds(5))
            {
                return _status;
            }

            var failed = _options.Mock.FailConnection;
            _status = new GenetecConnectionStatus(
                "Mock",
                "Mock",
                false,
                true,
                false,
                false,
                "NotApplicable",
                false,
                false,
                "Mock mode does not connect to a Genetec Media Gateway.",
                failed
                    ? "The mock adapter was configured to fail. No Genetec Directory was contacted."
                    : "Mock adapter is active. Camera names and recordings are synthetic and are not Genetec archives.",
                failed ? "Mock connection failure." : null,
                _time.GetUtcNow().UtcDateTime);

            _logger.LogInformation("Mock Genetec probe completed. DirectoryConnected={Connected}", false);
            return _status;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<TimeRange> BuildSequences(MockCameraDefinition camera, DateTime startUtc, DateTime endUtc)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var retentionStart = now - camera.Retention;
        var cursor = startUtc < retentionStart ? retentionStart : startUtc;
        var end = endUtc > now ? now : endUtc;
        if (end <= cursor)
        {
            return Array.Empty<TimeRange>();
        }

        if (!camera.DailyMaintenanceGap)
        {
            return [new TimeRange(cursor, end)];
        }

        var sequences = new List<TimeRange>();
        while (cursor < end)
        {
            var day = DateTime.SpecifyKind(cursor.Date, DateTimeKind.Utc);
            var gapStart = day.AddHours(3).AddMinutes(5);
            var gapEnd = gapStart.AddSeconds(30);
            if (gapEnd <= cursor)
            {
                gapStart = gapStart.AddDays(1);
                gapEnd = gapStart.AddSeconds(30);
            }

            if (gapStart >= end)
            {
                sequences.Add(new TimeRange(cursor, end));
                break;
            }

            if (gapStart > cursor)
            {
                sequences.Add(new TimeRange(cursor, gapStart));
            }

            cursor = gapEnd;
        }

        return sequences;
    }

    private static GenetecCameraInfo ToInfo(MockCameraDefinition camera) =>
        new(camera.Guid, camera.Name, camera.Location, camera.Online, camera.AccessGranted, true);
}
