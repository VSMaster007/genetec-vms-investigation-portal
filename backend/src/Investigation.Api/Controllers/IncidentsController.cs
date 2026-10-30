using Investigation.Application;
using Investigation.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Investigation.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrator,Investigator")]
[Route("api/incidents")]
public sealed class IncidentsController : PortalController
{
    private readonly IncidentService _incidents;

    public IncidentsController(IncidentService incidents)
    {
        _incidents = incidents;
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] string? incidentId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? camera,
        [FromQuery] string? eventType,
        [FromQuery] string? location,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseRange(from, to, out var range, out var error))
        {
            return error!;
        }

        return Ok(await _incidents.SearchAsync(new IncidentSearchCriteria(incidentId, range.From, range.To, camera, eventType, location, page, pageSize), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        FromResult(await _incidents.GetAsync(id, CurrentUserId, recordSelection: true, cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult> Search(
        [FromQuery] string? incidentId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? camera,
        [FromQuery] string? eventType,
        [FromQuery] string? location,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(incidentId) && string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to) &&
            string.IsNullOrWhiteSpace(camera) && string.IsNullOrWhiteSpace(eventType) && string.IsNullOrWhiteSpace(location))
        {
            return Problem(title: "SearchCriteriaRequired", detail: "Provide at least one search filter.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryParseRange(from, to, out var range, out var error))
        {
            return error!;
        }

        return Ok(await _incidents.SearchAsync(new IncidentSearchCriteria(incidentId, range.From, range.To, camera, eventType, location, page, pageSize), cancellationToken));
    }

    [HttpGet("facets")]
    public async Task<ActionResult> Facets([FromServices] IInvestigationStore store, CancellationToken cancellationToken) =>
        Ok(new
        {
            eventTypes = await store.ListEventTypesAsync(cancellationToken),
            locations = await store.ListLocationsAsync(cancellationToken)
        });

    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpPost("sync")]
    public async Task<ActionResult> Sync([FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken)
    {
        if (!TryParseRange(from, to, out var range, out var error))
        {
            return error!;
        }

        return Ok(await _incidents.SyncAsync(range.From, range.To, cancellationToken));
    }

    private bool TryParseRange(string? from, string? to, out (DateTime? From, DateTime? To) range, out ActionResult? error)
    {
        range = default;
        if (!TryParseUtc(from, out var fromUtc, out error) || !TryParseUtc(to, out var toUtc, out error))
        {
            return false;
        }

        if (fromUtc is not null && toUtc is not null && toUtc < fromUtc)
        {
            error = Problem(title: "InvalidTimestamp", detail: "The end of the range is before the start.", statusCode: StatusCodes.Status400BadRequest);
            return false;
        }

        range = (fromUtc, toUtc);
        return true;
    }
}
