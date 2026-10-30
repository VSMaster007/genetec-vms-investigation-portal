using Investigation.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Investigation.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrator,Investigator")]
public sealed class OperationsController : PortalController
{
    private readonly IGenetecService _genetec;
    private readonly CameraCatalogService _cameras;
    private readonly PlaybackService _playback;
    private readonly SystemStatusService _status;
    private readonly DashboardService _dashboard;
    private readonly AuditQueryService _audit;

    public OperationsController(
        IGenetecService genetec,
        CameraCatalogService cameras,
        PlaybackService playback,
        SystemStatusService status,
        DashboardService dashboard,
        AuditQueryService audit)
    {
        _genetec = genetec;
        _cameras = cameras;
        _playback = playback;
        _status = status;
        _dashboard = dashboard;
        _audit = audit;
    }

    [HttpGet("api/genetec/status")]
    public async Task<ActionResult> GenetecStatus(CancellationToken cancellationToken) =>
        Ok(await _genetec.GetConnectionStatusAsync(cancellationToken));

    [HttpPost("api/genetec/connect")]
    public async Task<ActionResult> Connect(CancellationToken cancellationToken)
    {
        var status = await _genetec.ConnectAsync(cancellationToken);
        if (status.DirectoryConnected || status.Mode == "Mock")
        {
            await _cameras.RefreshAsync(cancellationToken);
        }

        return Ok(status);
    }

    [HttpGet("api/genetec/cameras")]
    public async Task<ActionResult> GenetecCameras(CancellationToken cancellationToken) =>
        Ok(await _cameras.RefreshAsync(cancellationToken));

    [HttpGet("api/genetec/cameras/{id:guid}")]
    public async Task<ActionResult> GenetecCamera(Guid id, CancellationToken cancellationToken)
    {
        var camera = await _genetec.GetCameraByIdAsync(id, cancellationToken);
        return camera is null
            ? Problem(title: "CameraNotFound", detail: "The Genetec integration did not return that camera.", statusCode: StatusCodes.Status404NotFound)
            : Ok(camera);
    }

    [HttpPost("api/playback/resolve")]
    public async Task<ActionResult> Resolve([FromBody] PlaybackRequest request, CancellationToken cancellationToken) =>
        FromResult(await _playback.ResolveAsync(request.IncidentId, request.PreEventSeconds, request.PostEventSeconds, CurrentUserId, cancellationToken));

    [HttpPost("api/playback/session")]
    public async Task<ActionResult> Session([FromBody] PlaybackRequest request, CancellationToken cancellationToken) =>
        FromResult(await _playback.CreateSessionAsync(request.IncidentId, request.PreEventSeconds, request.PostEventSeconds, CurrentUserId, cancellationToken));

    [HttpGet("api/playback/availability")]
    public async Task<ActionResult> Availability([FromQuery] Guid genetecCameraGuid, [FromQuery] string startUtc, [FromQuery] string endUtc, CancellationToken cancellationToken)
    {
        if (!TryParseUtc(startUtc, out var start, out var error) || !TryParseUtc(endUtc, out var end, out error))
        {
            return error!;
        }

        if (start is null || end is null)
        {
            return Problem(title: "InvalidTimestamp", detail: "startUtc and endUtc are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        return FromResult(await _playback.GetAvailabilityAsync(genetecCameraGuid, start.Value, end.Value, cancellationToken));
    }

    [HttpPost("api/playback/token/{cameraGuid:guid}")]
    public async Task<ActionResult> Token(Guid cameraGuid, CancellationToken cancellationToken)
    {
        var result = await _playback.GetTokenAsync(cameraGuid, CurrentUserId, cancellationToken);
        if (!result.Succeeded || result.Value?.Token is null)
        {
            return FromResult(result);
        }

        return Ok(new { token = result.Value.Token });
    }

    [HttpGet("api/system/status")]
    public async Task<ActionResult> SystemStatus(CancellationToken cancellationToken) =>
        Ok(await _status.GetAsync(cancellationToken));

    [HttpGet("api/dashboard")]
    public async Task<ActionResult> Dashboard(CancellationToken cancellationToken) =>
        Ok(await _dashboard.GetAsync(cancellationToken));

    [HttpGet("api/audit")]
    public async Task<ActionResult> Audit([FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        Ok(await _audit.ListAsync(IsAdministrator ? null : CurrentUserId, take, cancellationToken));
}

public sealed class PlaybackRequest
{
    public Guid IncidentId { get; set; }
    public int? PreEventSeconds { get; set; }
    public int? PostEventSeconds { get; set; }
}
