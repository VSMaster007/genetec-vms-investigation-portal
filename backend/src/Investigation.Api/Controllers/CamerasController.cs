using System.ComponentModel.DataAnnotations;
using Investigation.Application;
using Investigation.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Investigation.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrator,Investigator")]
[Route("api/cameras")]
public sealed class CamerasController : PortalController
{
    private readonly CameraCatalogService _cameras;

    public CamerasController(CameraCatalogService cameras)
    {
        _cameras = cameras;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search, [FromQuery] bool includeMock = true, CancellationToken cancellationToken = default) =>
        Ok(await _cameras.ListAsync(search, includeMock, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        FromResult(await _cameras.GetAsync(id, cancellationToken));

    [HttpGet("mappings")]
    public async Task<ActionResult> Mappings(CancellationToken cancellationToken) =>
        Ok(await _cameras.GetMappingsAsync(cancellationToken));

    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpPost("mappings")]
    public async Task<ActionResult> CreateMapping([FromBody] MappingRequest request, CancellationToken cancellationToken) =>
        FromResult(await _cameras.CreateMappingAsync(request.ExternalCameraId, request.CameraId, cancellationToken));

    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpPut("mappings/{id:guid}")]
    public async Task<ActionResult> UpdateMapping(Guid id, [FromBody] MappingRequest request, CancellationToken cancellationToken) =>
        FromResult(await _cameras.UpdateMappingAsync(id, request.ExternalCameraId, request.CameraId, cancellationToken));

    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpDelete("mappings/{id:guid}")]
    public async Task<ActionResult> DeleteMapping(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _cameras.DeleteMappingAsync(id, cancellationToken);
        return deleted ? NoContent() : Problem(title: "MappingNotFound", detail: "The mapping was not found.", statusCode: StatusCodes.Status404NotFound);
    }
}

public sealed class MappingRequest
{
    [Required]
    public string ExternalCameraId { get; set; } = string.Empty;

    [Required]
    public Guid CameraId { get; set; }
}
