using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Investigation.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Genetec;

public sealed class WebSdkGenetecService : IGenetecService
{
    private readonly HttpClient _webSdk;
    private readonly HttpClient _mediaGateway;
    private readonly GenetecOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<WebSdkGenetecService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GenetecConnectionStatus? _status;

    public WebSdkGenetecService(
        HttpClient webSdk,
        HttpClient mediaGateway,
        IOptions<GenetecOptions> options,
        TimeProvider time,
        ILogger<WebSdkGenetecService> logger)
    {
        _webSdk = webSdk;
        _mediaGateway = mediaGateway;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public Task<GenetecConnectionStatus> ConnectAsync(CancellationToken cancellationToken) => ProbeAsync(force: true, cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _status = null;
        _logger.LogInformation("Cleared the cached Web SDK connection status. Web SDK sessions are per request.");
        return Task.CompletedTask;
    }

    public Task<GenetecConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken) =>
        ProbeAsync(force: false, cancellationToken);

    public async Task<IReadOnlyList<GenetecCameraInfo>> GetCamerasAsync(CancellationToken cancellationToken)
    {
        var rows = new List<GenetecCameraInfo>();
        var page = 1;
        const int pageSize = 100;
        Guid? overlap = null;

        while (page <= 20)
        {
            var relative = $"report/EntityConfiguration?q=EntityTypes@Camera,Page={page},PageSize={pageSize},ReturnFields@Name";
            var call = await GetWebAsync(relative, cancellationToken);
            if (!call.Succeeded)
            {
                _logger.LogWarning("Camera discovery failed with {Code}.", call.ErrorCode);
                break;
            }

            var parsed = WebSdkJson.Read(call.Body!);
            if (parsed.Result is null || parsed.Result.Value.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var batch = new List<GenetecCameraInfo>();
            foreach (var item in parsed.Result.Value.EnumerateArray())
            {
                if (!item.TryGetProperty("Guid", out var guidProperty) || !Guid.TryParse(guidProperty.GetString(), out var guid))
                {
                    continue;
                }

                var name = item.TryGetProperty("Name", out var nameProperty) ? nameProperty.GetString() : null;
                batch.Add(new GenetecCameraInfo(guid, string.IsNullOrWhiteSpace(name) ? guid.ToString("D") : name, null, null, true, false));
            }

            if (overlap is not null && batch.Count > 0 && batch[0].Guid == overlap)
            {
                batch.RemoveAt(0);
            }

            if (string.Equals(parsed.Status, "TooManyResults", StringComparison.OrdinalIgnoreCase) && batch.Count > 0)
            {
                overlap = batch[^1].Guid;
                batch.RemoveAt(batch.Count - 1);
            }
            else
            {
                overlap = null;
            }

            rows.AddRange(batch);
            if (!string.Equals(parsed.Status, "TooManyResults", StringComparison.OrdinalIgnoreCase) || batch.Count == 0)
            {
                break;
            }

            page++;
        }

        var enriched = new List<GenetecCameraInfo>(rows.Count);
        foreach (var camera in rows.Take(100))
        {
            enriched.Add(await ReadCameraAsync(camera.Guid, cancellationToken) ?? camera);
        }

        if (rows.Count > enriched.Count)
        {
            enriched.AddRange(rows.Skip(enriched.Count));
        }

        return enriched;
    }

    public Task<GenetecCameraInfo?> GetCameraByIdAsync(Guid cameraGuid, CancellationToken cancellationToken) =>
        ReadCameraAsync(cameraGuid, cancellationToken);

    public async Task<RecordingQueryResult> GetRecordingAvailabilityAsync(Guid cameraGuid, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var start = TimestampNormalizer.FormatUtc(startUtc);
        var end = TimestampNormalizer.FormatUtc(endUtc);
        var relative = $"report/VideoSequence?q=Cameras@{cameraGuid:D},TimeRange.SetTimeRange({start},{end})";
        var call = await GetWebAsync(relative, cancellationToken);
        if (call.StatusCode == 403)
        {
            return new RecordingQueryResult(false, "InsufficientPermissions", "Security Center refused the recording query.", false, null, Array.Empty<TimeRange>());
        }

        if (!call.Succeeded || call.Body is null)
        {
            return new RecordingQueryResult(false, call.ErrorCode ?? "RecordingQueryFailed", call.ErrorMessage, false, null, Array.Empty<TimeRange>());
        }

        var parsed = WebSdkJson.Read(call.Body);
        if (!string.IsNullOrWhiteSpace(parsed.Message) && parsed.Message.Contains("DispatchFailed", StringComparison.OrdinalIgnoreCase))
        {
            return new RecordingQueryResult(false, "RecordingQueryFailed", parsed.Message, false, null, Array.Empty<TimeRange>());
        }

        if (parsed.Result is null || parsed.Result.Value.ValueKind != JsonValueKind.Array)
        {
            var failed = !string.Equals(parsed.Status, "Ok", StringComparison.OrdinalIgnoreCase);
            return new RecordingQueryResult(!failed, failed ? "RecordingQueryFailed" : null, parsed.Message, false, null, Array.Empty<TimeRange>());
        }

        var sequences = new List<TimeRange>();
        foreach (var item in parsed.Result.Value.EnumerateArray())
        {
            if (!item.TryGetProperty("StartTime", out var startProperty) || !item.TryGetProperty("EndTime", out var endProperty))
            {
                continue;
            }

            try
            {
                sequences.Add(new TimeRange(
                    TimestampNormalizer.ParseUtc(startProperty.GetString() ?? string.Empty),
                    TimestampNormalizer.ParseUtc(endProperty.GetString() ?? string.Empty)));
            }
            catch (Investigation.Domain.TimestampNormalizationException exception)
            {
                _logger.LogWarning("Skipped a video sequence with an invalid timestamp: {Reason}", exception.Message);
            }
        }

        return new RecordingQueryResult(true, null, parsed.Message, false, null, sequences);
    }

    public async Task<GenetecPlaybackHandle> ResolvePlaybackAsync(Guid cameraGuid, DateTime seekUtc, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var endpoint = GetPlaybackEndpoint();
        var camera = await GetCameraByIdAsync(cameraGuid, cancellationToken);
        if (camera is null)
        {
            return new GenetecPlaybackHandle(false, "CameraNotFound", "The Web SDK did not return this camera.", endpoint, new RecordingQueryResult(false, "CameraNotFound", "Camera not found.", false, null, Array.Empty<TimeRange>()));
        }

        if (!camera.AccessGranted)
        {
            return new GenetecPlaybackHandle(false, "InsufficientPermissions", "Playback permission was denied for this camera.", endpoint, new RecordingQueryResult(false, "InsufficientPermissions", "Permission denied.", false, null, Array.Empty<TimeRange>()));
        }

        var recording = await GetRecordingAvailabilityAsync(cameraGuid, startUtc, endUtc, cancellationToken);
        var succeeded = recording.Succeeded && endpoint.SupportsBrowserPlayback;
        return new GenetecPlaybackHandle(succeeded, succeeded ? null : endpoint.SupportsBrowserPlayback ? recording.ErrorCode : "MediaGatewayNotConfigured", succeeded ? null : endpoint.Notice, endpoint, recording);
    }

    public async Task<MediaGatewayTokenResult> GetPlaybackTokenAsync(Guid cameraGuid, CancellationToken cancellationToken)
    {
        var server = _options.MediaGateway.ServerEndpoint;
        if (string.IsNullOrWhiteSpace(server))
        {
            return new MediaGatewayTokenResult(false, null, "MediaGatewayNotConfigured", "Set Genetec:MediaGateway:ServerEndpoint before requesting a token.");
        }

        var username = First(_options.MediaGateway.Username, _options.WebSdk.Username);
        var certificate = First(_options.MediaGateway.SdkCertificate, _options.WebSdk.ApplicationId);
        var password = First(_options.MediaGateway.Password, _options.WebSdk.Password);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(certificate) || string.IsNullOrWhiteSpace(password))
        {
            return new MediaGatewayTokenResult(false, null, "MediaGatewayCredentialsMissing", "Media Gateway token requests need a username, SDK certificate, and password.");
        }

        var url = $"{server.TrimEnd('/')}/v2/token/{cameraGuid:D}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = GenetecCredentials.Header(username, certificate, password);
        try
        {
            using var response = await _mediaGateway.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
            {
                _logger.LogWarning("Media Gateway token request for camera {CameraGuid} failed with status {Status}.", cameraGuid, (int)response.StatusCode);
                return new MediaGatewayTokenResult(false, null, "TokenRejected", "The Media Gateway did not issue a playback token.");
            }

            return new MediaGatewayTokenResult(true, body.Trim(), null, null);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Media Gateway token request failed for camera {CameraGuid}.", cameraGuid);
            return new MediaGatewayTokenResult(false, null, "MediaGatewayUnreachable", "The Media Gateway could not be reached for a playback token.");
        }
    }

    public PlaybackEndpointInfo GetPlaybackEndpoint()
    {
        var browser = First(_options.MediaGateway.BrowserEndpoint, _options.MediaGateway.ServerEndpoint);
        if (string.IsNullOrWhiteSpace(browser))
        {
            return new PlaybackEndpointInfo("Real", "None", null, null, false, false, "The Media Gateway browser endpoint is not configured.");
        }

        var endpoint = browser.TrimEnd('/');
        return new PlaybackEndpointInfo(
            "Real",
            "GenetecWebPlayer",
            endpoint,
            $"{endpoint}/v2/files/gwp.js",
            true,
            false,
            "Playback uses the Genetec Web Player connected to the configured Media Gateway.");
    }

    private async Task<GenetecCameraInfo?> ReadCameraAsync(Guid cameraGuid, CancellationToken cancellationToken)
    {
        var call = await GetWebAsync($"entity?q=entity={cameraGuid:D},Name,Description,IsOnline", cancellationToken);
        if (call.StatusCode is 403 or 401)
        {
            return new GenetecCameraInfo(cameraGuid, cameraGuid.ToString("D"), null, null, false, false);
        }

        if (!call.Succeeded || call.Body is null)
        {
            return call.StatusCode == 404 ? null : null;
        }

        var parsed = WebSdkJson.Read(call.Body);
        if (parsed.Result is null || parsed.Result.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = parsed.Result.Value;
        var name = result.TryGetProperty("Name", out var nameProperty) ? nameProperty.GetString() : null;
        var description = result.TryGetProperty("Description", out var descriptionProperty) ? descriptionProperty.GetString() : null;
        bool? online = null;
        if (result.TryGetProperty("IsOnline", out var onlineProperty))
        {
            online = onlineProperty.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(onlineProperty.GetString(), out var parsedOnline) => parsedOnline,
                _ => null
            };
        }

        return new GenetecCameraInfo(cameraGuid, string.IsNullOrWhiteSpace(name) ? cameraGuid.ToString("D") : name!, description, online, true, false);
    }

    private async Task<GenetecConnectionStatus> ProbeAsync(bool force, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!force && _status is not null && _time.GetUtcNow() - _status.CheckedAtUtc < TimeSpan.FromSeconds(10))
            {
                return _status;
            }

            var directory = await ProbeDirectoryAsync(cancellationToken);
            var media = await ProbeMediaGatewayAsync(cancellationToken);
            _status = directory with
            {
                MediaGatewayAttempted = media.Attempted,
                MediaGatewayReachable = media.Reachable,
                MediaGatewayDetail = media.Detail
            };
            return _status;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<GenetecConnectionStatus> ProbeDirectoryAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        if (string.IsNullOrWhiteSpace(_options.WebSdk.BaseUrl))
        {
            return NotConnected(now, "NotConfigured", "Genetec:WebSdk:BaseUrl is empty. The Web SDK role was not contacted.");
        }

        if (string.IsNullOrWhiteSpace(_options.WebSdk.Username) || string.IsNullOrWhiteSpace(_options.WebSdk.ApplicationId) || string.IsNullOrWhiteSpace(_options.WebSdk.Password))
        {
            return NotConnected(now, "NotConfigured", "Web SDK username, application id, and password are required before authentication is attempted.");
        }

        var call = await GetWebAsync(string.Empty, cancellationToken);
        if (call.TransportFailure)
        {
            return NotConnected(now, "TlsOrNetworkFailure", call.ErrorMessage ?? "The Web SDK endpoint could not be reached.");
        }

        if (call.StatusCode is 401 or 403)
        {
            return NotConnected(now, "Rejected", "Security Center rejected the Web SDK credentials, SDK certificate, or the Log on using the SDK privilege.");
        }

        if (!call.Succeeded || call.Body is null)
        {
            return NotConnected(now, "UnexpectedResponse", call.ErrorMessage ?? "The Web SDK returned an unexpected response.");
        }

        var parsed = WebSdkJson.Read(call.Body);
        var ok = string.Equals(parsed.Status, "Ok", StringComparison.OrdinalIgnoreCase);
        return new GenetecConnectionStatus(
            "Real",
            "WebSdk",
            false,
            true,
            ok,
            ok,
            ok ? "Presented" : "Unverified",
            false,
            false,
            null,
            ok ? $"Web SDK role responded at {_time.GetUtcNow():O}." : parsed.Message ?? "Web SDK status was not Ok.",
            ok ? null : parsed.Message ?? "Web SDK authentication did not succeed.",
            now);
    }

    private async Task<(bool Attempted, bool Reachable, string Detail)> ProbeMediaGatewayAsync(CancellationToken cancellationToken)
    {
        var endpoint = First(_options.MediaGateway.ServerEndpoint, _options.MediaGateway.BrowserEndpoint);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return (false, false, "Genetec:MediaGateway:ServerEndpoint is empty.");
        }

        var url = $"{endpoint.TrimEnd('/')}/v2/files/gwp.js";
        try
        {
            using var response = await _mediaGateway.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return (true, true, "Media Gateway returned the Genetec Web Player script.");
            }

            return (true, false, $"Media Gateway responded with {(int)response.StatusCode} for gwp.js. Browser playback is not verified.");
        }
        catch (Exception exception)
        {
            return (true, false, $"Media Gateway probe failed: {exception.Message}");
        }
    }

    private async Task<WebCall> GetWebAsync(string relative, CancellationToken cancellationToken)
    {
        if (_webSdk.BaseAddress is null)
        {
            return WebCall.Failed("WebSdkNotConfigured", "The Web SDK HTTP client has no base address.", 0, true);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, relative);
        request.Headers.Authorization = GenetecCredentials.Header(_options.WebSdk.Username, _options.WebSdk.ApplicationId, _options.WebSdk.Password);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/json"));
        try
        {
            using var response = await _webSdk.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return WebCall.Success(body, (int)response.StatusCode);
            }

            return WebCall.Failed("WebSdkHttpError", $"Web SDK returned {(int)response.StatusCode}.", (int)response.StatusCode, false);
        }
        catch (Exception exception)
        {
            return WebCall.Failed("WebSdkUnreachable", exception.Message, 0, true);
        }
    }

    private GenetecConnectionStatus NotConnected(DateTime checkedAt, string certificateStatus, string detail) =>
        new("Real", "WebSdk", false, true, false, false, certificateStatus, false, false, null, detail, detail, checkedAt);

    private static string First(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

    private sealed record WebCall(bool Succeeded, string? Body, int StatusCode, string? ErrorCode, string? ErrorMessage, bool TransportFailure)
    {
        public static WebCall Success(string body, int statusCode) => new(true, body, statusCode, null, null, false);
        public static WebCall Failed(string code, string message, int statusCode, bool transport) => new(false, null, statusCode, code, message, transport);
    }
}
