using Investigation.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Application;

public sealed class PlaybackService
{
    private readonly IInvestigationStore _store;
    private readonly IGenetecService _genetec;
    private readonly PlaybackOptions _playback;
    private readonly DemoMediaOptions _demo;
    private readonly TimeProvider _time;
    private readonly ILogger<PlaybackService> _logger;

    public PlaybackService(
        IInvestigationStore store,
        IGenetecService genetec,
        IOptions<PlaybackOptions> playback,
        IOptions<DemoMediaOptions> demo,
        TimeProvider time,
        ILogger<PlaybackService> logger)
    {
        _store = store;
        _genetec = genetec;
        _playback = playback.Value;
        _demo = demo.Value;
        _time = time;
        _logger = logger;
    }

    public Task<OperationResult<PlaybackSessionDto>> ResolveAsync(
        Guid incidentId,
        int? preEventSeconds,
        int? postEventSeconds,
        Guid userId,
        CancellationToken cancellationToken) =>
        BuildAsync(incidentId, preEventSeconds, postEventSeconds, userId, initializeSession: false, cancellationToken);

    public Task<OperationResult<PlaybackSessionDto>> CreateSessionAsync(
        Guid incidentId,
        int? preEventSeconds,
        int? postEventSeconds,
        Guid userId,
        CancellationToken cancellationToken) =>
        BuildAsync(incidentId, preEventSeconds, postEventSeconds, userId, initializeSession: true, cancellationToken);

    public async Task<OperationResult<RecordingAvailabilityDto>> GetAvailabilityAsync(
        Guid genetecCameraGuid,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        if (startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc || endUtc <= startUtc)
        {
            return OperationResult<RecordingAvailabilityDto>.Fail(
                "InvalidTimestamp",
                "Availability requires a UTC start and a later UTC end.",
                400);
        }

        var camera = await _store.FindCameraByGenetecGuidAsync(genetecCameraGuid, cancellationToken);
        if (camera is null)
        {
            return OperationResult<RecordingAvailabilityDto>.Fail("CameraNotFound", "The camera is not in the local catalog.", 404);
        }

        var info = await _genetec.GetCameraByIdAsync(genetecCameraGuid, cancellationToken);
        var recording = await _genetec.GetRecordingAvailabilityAsync(genetecCameraGuid, startUtc, endUtc, cancellationToken);
        var midpoint = startUtc.AddTicks((endUtc - startUtc).Ticks / 2);
        var assessment = RecordingWindowAnalyzer.Assess(
            startUtc,
            midpoint,
            endUtc,
            _time.GetUtcNow().UtcDateTime,
            recording.Retention,
            info is not null,
            info?.AccessGranted ?? false,
            recording.Succeeded,
            recording.ErrorCode,
            recording.Sequences);

        return OperationResult<RecordingAvailabilityDto>.Ok(ToAvailability(assessment, recording.IsMock || camera.IsMock));
    }

    public async Task<OperationResult<MediaGatewayTokenResult>> GetTokenAsync(Guid genetecCameraGuid, Guid userId, CancellationToken cancellationToken)
    {
        var endpoint = _genetec.GetPlaybackEndpoint();
        if (!endpoint.SupportsBrowserPlayback)
        {
            await AuditAsync(userId, null, AuditActions.PlaybackAuthorizationFailed, "A playback token was requested while browser playback is not available.", cancellationToken);
            return OperationResult<MediaGatewayTokenResult>.Fail(
                "PlaybackTokenUnavailable",
                endpoint.Notice,
                409);
        }

        var info = await _genetec.GetCameraByIdAsync(genetecCameraGuid, cancellationToken);
        if (info is null)
        {
            return OperationResult<MediaGatewayTokenResult>.Fail("CameraNotFound", "The camera was not returned by Genetec.", 404);
        }

        if (!info.AccessGranted)
        {
            await AuditAsync(userId, null, AuditActions.PlaybackAuthorizationFailed, $"Playback permission denied for camera {genetecCameraGuid:D}.", cancellationToken);
            return OperationResult<MediaGatewayTokenResult>.Fail("InsufficientPermissions", "Playback permission was denied for this camera.", 403);
        }

        var token = await _genetec.GetPlaybackTokenAsync(genetecCameraGuid, cancellationToken);
        if (!token.Succeeded)
        {
            await AuditAsync(userId, null, AuditActions.PlaybackAuthorizationFailed, $"Media Gateway refused a token for camera {genetecCameraGuid:D}.", cancellationToken);
            return OperationResult<MediaGatewayTokenResult>.Fail(token.ErrorCode ?? "TokenRejected", token.ErrorMessage ?? "The Media Gateway did not issue a token.", 502);
        }

        _logger.LogInformation("Issued a Media Gateway token for camera {CameraGuid} to user {UserId}. The token value was not logged.", genetecCameraGuid, userId);
        return OperationResult<MediaGatewayTokenResult>.Ok(token);
    }

    private async Task<OperationResult<PlaybackSessionDto>> BuildAsync(
        Guid incidentId,
        int? preEventSeconds,
        int? postEventSeconds,
        Guid userId,
        bool initializeSession,
        CancellationToken cancellationToken)
    {
        var incident = await _store.FindIncidentAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return OperationResult<PlaybackSessionDto>.Fail("IncidentNotFound", "The incident was not found.", 404);
        }

        PlaybackWindow window;
        try
        {
            window = PlaybackWindowCalculator.Create(
                incident.EventTimestampUtc,
                preEventSeconds ?? _playback.DefaultPreEventSeconds,
                postEventSeconds ?? _playback.DefaultPostEventSeconds,
                _playback.MaxWindowSeconds);
        }
        catch (TimestampNormalizationException exception)
        {
            await AuditAsync(userId, incident.Id, AuditActions.IntegrationError, exception.Message, cancellationToken);
            return OperationResult<PlaybackSessionDto>.Fail("InvalidTimestamp", exception.Message, 400);
        }

        var mapping = await _store.FindMappingByExternalIdAsync(incident.ExternalCameraId, cancellationToken);
        if (mapping?.Camera is null)
        {
            await AuditAsync(
                userId,
                incident.Id,
                AuditActions.IntegrationError,
                $"No camera mapping exists for {incident.ExternalCameraId}.",
                cancellationToken);
            return OperationResult<PlaybackSessionDto>.Fail(
                "MissingCameraMapping",
                $"External camera {incident.ExternalCameraId} is not mapped to a Genetec camera.",
                409);
        }

        var camera = mapping.Camera;
        var endpoint = _genetec.GetPlaybackEndpoint();
        if (endpoint.Mode == "Real" && camera.IsMock)
        {
            await AuditAsync(userId, incident.Id, AuditActions.IntegrationError, "A real playback request targeted a mock camera.", cancellationToken);
            return OperationResult<PlaybackSessionDto>.Fail(
                "MockCameraRejected",
                "This mapping points at a mock camera. Refresh the catalog from Genetec and map a real camera.",
                409);
        }

        var info = await _genetec.GetCameraByIdAsync(camera.GenetecCameraGuid, cancellationToken);
        if (info is null)
        {
            await AuditAsync(userId, incident.Id, AuditActions.IntegrationError, $"Camera {camera.GenetecCameraGuid:D} was not found.", cancellationToken);
            return OperationResult<PlaybackSessionDto>.Fail("CameraNotFound", "The mapped Genetec camera was not found.", 404);
        }

        if (!info.AccessGranted)
        {
            await AuditAsync(userId, incident.Id, AuditActions.PlaybackAuthorizationFailed, $"Playback permission denied for {camera.CameraName}.", cancellationToken);
            return OperationResult<PlaybackSessionDto>.Fail("InsufficientPermissions", "The integration account cannot play this camera.", 403);
        }

        await AuditAsync(userId, incident.Id, AuditActions.CameraSelected, $"Resolved {incident.ExternalCameraId} to {camera.CameraName} ({camera.GenetecCameraGuid:D}).", cancellationToken);

        var recording = await _genetec.GetRecordingAvailabilityAsync(camera.GenetecCameraGuid, window.StartUtc, window.EndUtc, cancellationToken);
        var assessment = RecordingWindowAnalyzer.Assess(
            window.StartUtc,
            window.EventUtc,
            window.EndUtc,
            _time.GetUtcNow().UtcDateTime,
            recording.Retention,
            cameraFound: true,
            accessGranted: true,
            recording.Succeeded,
            recording.ErrorCode,
            recording.Sequences);

        if (!assessment.CanAttemptPlayback)
        {
            await AuditAsync(userId, incident.Id, AuditActions.RecordingUnavailable, assessment.Message, cancellationToken);
        }
        else if (initializeSession)
        {
            await AuditAsync(
                userId,
                incident.Id,
                AuditActions.PlaybackInitialized,
                $"Playback window {TimestampNormalizer.FormatUtc(window.StartUtc)} to {TimestampNormalizer.FormatUtc(window.EndUtc)} for camera {camera.GenetecCameraGuid:D}.",
                cancellationToken);
        }

        var canPlay = assessment.CanAttemptPlayback && (endpoint.SupportsBrowserPlayback || endpoint.IsMock);
        var notice = endpoint.Notice;
        if (!assessment.CanAttemptPlayback)
        {
            notice = assessment.Message;
        }

        var pre = preEventSeconds ?? _playback.DefaultPreEventSeconds;
        var post = postEventSeconds ?? _playback.DefaultPostEventSeconds;
        var demo = canPlay && endpoint.IsMock
            ? BuildDemo(incident.ExternalCameraId, window.EventUtc, pre, post)
            : null;
        if (demo is not null)
        {
            notice = "DEMO MODE — sample video. This MP4 is not a Genetec archived recording.";
        }

        var session = new PlaybackSessionDto(
            endpoint.Mode,
            demo is not null ? "DemoMp4" : canPlay && endpoint.SupportsBrowserPlayback ? endpoint.Provider : "None",
            endpoint.SupportsBrowserPlayback && !endpoint.IsMock && assessment.CanAttemptPlayback,
            canPlay,
            notice,
            incident.Id,
            incident.ExternalIncidentId,
            incident.Title,
            incident.ExternalCameraId,
            camera.CameraName,
            camera.Id,
            camera.GenetecCameraGuid,
            canPlay && endpoint.SupportsBrowserPlayback ? endpoint.MediaGatewayEndpoint : null,
            canPlay && endpoint.SupportsBrowserPlayback ? endpoint.GwpScriptUrl : null,
            canPlay && endpoint.SupportsBrowserPlayback ? $"/api/playback/token/{camera.GenetecCameraGuid:D}" : null,
            window.EventUtc,
            window.StartUtc,
            window.EndUtc,
            pre,
            post,
            ToAvailability(assessment, recording.IsMock || camera.IsMock),
            demo);

        return OperationResult<PlaybackSessionDto>.Ok(session);
    }

    private async Task AuditAsync(Guid userId, Guid? incidentId, string action, string details, CancellationToken cancellationToken)
    {
        if (await _store.HasRecentAuditAsync(userId, incidentId, action, TimeSpan.FromSeconds(3), cancellationToken))
        {
            return;
        }

        await _store.AddAuditAsync(new AuditEntry(userId, incidentId, action, details), cancellationToken);
    }

    private DemoRecordingDto? BuildDemo(string externalCameraId, DateTime incidentUtc, int preEventSeconds, int postEventSeconds)
    {
        if (!_demo.Enabled || !externalCameraId.Equals(_demo.ExternalCameraId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        DateTime anchor;
        try
        {
            anchor = TimestampNormalizer.ParseUtc(_demo.AnchorTimestampUtc);
        }
        catch (TimestampNormalizationException)
        {
            return null;
        }

        DateTime recordingStart;
        try
        {
            recordingStart = DemoTimestampMapper.RecordingStartForAnchor(anchor, _demo.IncidentOffsetSeconds);
        }
        catch (TimestampNormalizationException)
        {
            return null;
        }

        var timeline = DemoTimestampMapper.Map(recordingStart, _demo.DurationSeconds, incidentUtc, preEventSeconds, postEventSeconds);
        if (timeline is null)
        {
            return null;
        }

        return new DemoRecordingDto(
            _demo.MediaUrl,
            _demo.SourceName,
            _demo.SourceUrl,
            _demo.CameraName,
            _demo.ExternalCameraId,
            _demo.Location,
            _demo.CameraType,
            _demo.CameraStatus,
            "Demo",
            timeline.RecordingStartUtc,
            timeline.RecordingEndUtc,
            timeline.DurationSeconds,
            timeline.IncidentOffsetSeconds,
            timeline.PlaybackStartOffsetSeconds,
            timeline.PlaybackEndOffsetSeconds,
            timeline.SeekOffsetSeconds,
            "HTML5 Video");
    }

    private static RecordingAvailabilityDto ToAvailability(RecordingAssessment assessment, bool isMock) =>
        new(assessment.State, assessment.CoversEvent, isMock, assessment.Message, assessment.Sequences, assessment.Gaps);
}
