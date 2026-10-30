#if GENETEC_PLATFORM_SDK
using System.Data;
using Genetec.Sdk;
using Genetec.Sdk.Entities;
using Genetec.Sdk.Queries;
using Investigation.Application;
using Investigation.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Genetec;

/// <summary>
/// Platform SDK adapter. Compiled only when GSC_SDK_CORE points at an installed
/// Security Center SDK and the build runs on Windows. Members used here are taken
/// from the Genetec DAP guides for Engine logon, certificate validation, and ReportManager.
/// </summary>
public sealed class PlatformSdkGenetecService : IGenetecService, IDisposable
{
    private readonly GenetecOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<PlatformSdkGenetecService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Engine? _engine;
    private GenetecConnectionStatus? _status;

    public PlatformSdkGenetecService(IOptions<GenetecOptions> options, TimeProvider time, ILogger<PlatformSdkGenetecService> logger)
    {
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<GenetecConnectionStatus> ConnectAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            DisconnectEngine();
            var engine = new Engine();
            if (!string.IsNullOrWhiteSpace(_options.Platform.ApplicationId))
            {
                engine.ClientCertificate = _options.Platform.ApplicationId;
            }

            engine.LoginManager.RequestDirectoryCertificateValidation += (_, args) =>
            {
                _logger.LogInformation(
                    "Directory certificate thumbprint {Thumbprint} for {Host}. Accept configured: {Accept}",
                    args.DirectoryCertificate.Thumbprint,
                    args.DirectoryHostName,
                    _options.Platform.AcceptDirectoryCertificate);
                args.AcceptDirectory = _options.Platform.AcceptDirectoryCertificate;
            };

            _engine = engine;
            ConnectionStateCode result;
            try
            {
                result = await engine.LogOnAsync(_options.Platform.Directory, _options.Platform.Username, _options.Platform.Password);
            }
            catch (Exception exception)
            {
                _status = Failed(exception.Message, "LogonException");
                return _status;
            }

            var connected = result == ConnectionStateCode.Success && engine.LoginManager.IsConnected;
            _status = new GenetecConnectionStatus(
                "Real",
                "Platform",
                true,
                true,
                connected,
                connected,
                connected ? "Presented" : result.ToString(),
                false,
                false,
                "Browser playback still uses the Media Gateway Web Player. The Platform SDK does not embed video in the browser.",
                connected ? "Platform SDK logon returned Success." : $"Platform SDK logon returned {result}.",
                connected ? null : result.ToString(),
                _time.GetUtcNow().UtcDateTime);
            return _status;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        DisconnectEngine();
        _status = null;
        return Task.CompletedTask;
    }

    public async Task<GenetecConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken)
    {
        if (_status is null || _engine is null || !_engine.LoginManager.IsConnected)
        {
            return await ConnectAsync(cancellationToken);
        }

        return _status with { DirectoryConnected = _engine.LoginManager.IsConnected, Authenticated = _engine.LoginManager.IsConnected };
    }

    public async Task<IReadOnlyList<GenetecCameraInfo>> GetCamerasAsync(CancellationToken cancellationToken)
    {
        var status = await GetConnectionStatusAsync(cancellationToken);
        if (!status.DirectoryConnected || _engine is null)
        {
            return Array.Empty<GenetecCameraInfo>();
        }

        var query = (EntityConfigurationQuery)_engine.ReportManager.CreateReportQuery(ReportType.EntityConfiguration);
        query.EntityTypeFilter.Add(EntityType.Camera);
        query.Page = 1;
        query.PageSize = 200;
        query.MaximumResultCount = 200;
        var results = await Task.Factory.FromAsync(query.BeginQuery, query.EndQuery, null);
        if (!results.Success || results.Data is null)
        {
            _logger.LogWarning("Entity configuration query failed: {Error}", results.Error);
            return Array.Empty<GenetecCameraInfo>();
        }

        var cameras = new List<GenetecCameraInfo>();
        foreach (DataRow row in results.Data.Rows)
        {
            var guid = row.Field<Guid>(nameof(Guid));
            var entity = _engine.GetEntity<Camera>(guid);
            if (entity is null)
            {
                continue;
            }

            cameras.Add(new GenetecCameraInfo(guid, entity.Name, null, null, true, false));
        }

        return cameras;
    }

    public async Task<GenetecCameraInfo?> GetCameraByIdAsync(Guid cameraGuid, CancellationToken cancellationToken)
    {
        var cameras = await GetCamerasAsync(cancellationToken);
        return cameras.FirstOrDefault(camera => camera.Guid == cameraGuid) ??
               (_engine?.GetEntity<Camera>(cameraGuid) is { } entity
                   ? new GenetecCameraInfo(cameraGuid, entity.Name, null, null, true, false)
                   : null);
    }

    public async Task<RecordingQueryResult> GetRecordingAvailabilityAsync(Guid cameraGuid, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var status = await GetConnectionStatusAsync(cancellationToken);
        if (!status.DirectoryConnected || _engine is null)
        {
            return new RecordingQueryResult(false, "NotConnected", status.Error ?? "Platform SDK is not connected.", false, null, Array.Empty<TimeRange>());
        }

        var query = (SequenceQuery)_engine.ReportManager.CreateReportQuery(ReportType.VideoSequence, null);
        query.MaximumResultCount = 500;
        query.TimeRange.SetTimeRange(startUtc, endUtc);
        var results = await Task.Factory.FromAsync(query.BeginQuery, query.EndQuery, null);
        if (!results.Success || results.Data is null)
        {
            return new RecordingQueryResult(false, "RecordingQueryFailed", results.Error.ToString(), false, null, Array.Empty<TimeRange>());
        }

        if (!results.Data.Columns.Contains("CameraGuid") || !results.Data.Columns.Contains("StartTime") || !results.Data.Columns.Contains("EndTime"))
        {
            var columns = string.Join(", ", results.Data.Columns.Cast<DataColumn>().Select(column => column.ColumnName));
            return new RecordingQueryResult(
                false,
                "UnverifiedQuerySchema",
                $"The installed SDK returned VideoSequence columns [{columns}]. This build only reads the documented CameraGuid, StartTime, and EndTime fields.",
                false,
                null,
                Array.Empty<TimeRange>());
        }

        var sequences = new List<TimeRange>();
        foreach (DataRow row in results.Data.Rows)
        {
            if (row.Field<Guid>("CameraGuid") != cameraGuid)
            {
                continue;
            }

            if (TryRange(row["StartTime"], row["EndTime"], out var range))
            {
                sequences.Add(range);
            }
        }

        return new RecordingQueryResult(true, null, null, false, null, sequences);
    }

    public async Task<GenetecPlaybackHandle> ResolvePlaybackAsync(Guid cameraGuid, DateTime seekUtc, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var recording = await GetRecordingAvailabilityAsync(cameraGuid, startUtc, endUtc, cancellationToken);
        return new GenetecPlaybackHandle(recording.Succeeded, recording.ErrorCode, recording.ErrorMessage, GetPlaybackEndpoint(), recording);
    }

    public Task<MediaGatewayTokenResult> GetPlaybackTokenAsync(Guid cameraGuid, CancellationToken cancellationToken) =>
        Task.FromResult(new MediaGatewayTokenResult(
            false,
            null,
            "UseWebSdkTransport",
            "Media Gateway tokens are requested with the documented /v2/token endpoint. Use Genetec:RealTransport=WebSdk for browser playback, or call the token endpoint from the Web SDK host."));

    public PlaybackEndpointInfo GetPlaybackEndpoint()
    {
        var browser = string.IsNullOrWhiteSpace(_options.MediaGateway.BrowserEndpoint)
            ? _options.MediaGateway.ServerEndpoint
            : _options.MediaGateway.BrowserEndpoint;
        if (string.IsNullOrWhiteSpace(browser))
        {
            return new PlaybackEndpointInfo("Real", "None", null, null, false, false, "Platform SDK logon does not provide a browser media URL. Configure the Media Gateway endpoint.");
        }

        var endpoint = browser.TrimEnd('/');
        return new PlaybackEndpointInfo("Real", "GenetecWebPlayer", endpoint, $"{endpoint}/v2/files/gwp.js", true, false, "Load gwp.js from the Media Gateway. The Platform SDK is not a browser player.");
    }

    public void Dispose() => DisconnectEngine();

    private void DisconnectEngine()
    {
        if (_engine is null)
        {
            return;
        }

        if (_engine.LoginManager.IsConnected)
        {
            _engine.LogOff();
        }

        _engine.Dispose();
        _engine = null;
    }

    private GenetecConnectionStatus Failed(string detail, string certificateStatus) =>
        new("Real", "Platform", true, true, false, false, certificateStatus, false, false, null, detail, detail, _time.GetUtcNow().UtcDateTime);

    private static bool TryRange(object startValue, object endValue, out TimeRange range)
    {
        range = default!;
        if (startValue is not DateTime start || endValue is not DateTime end)
        {
            return false;
        }

        range = new TimeRange(DateTime.SpecifyKind(start.ToUniversalTime(), DateTimeKind.Utc), DateTime.SpecifyKind(end.ToUniversalTime(), DateTimeKind.Utc));
        return true;
    }
}
#else
namespace Investigation.Genetec;

internal static class PlatformSdkGenetecService
{
}
#endif
