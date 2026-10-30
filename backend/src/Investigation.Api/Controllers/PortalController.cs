using System.Security.Claims;
using Investigation.Application;
using Investigation.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Investigation.Api.Controllers;

public abstract class PortalController : ControllerBase
{
    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected bool IsAdministrator => User.IsInRole(nameof(UserRole.Administrator));

    protected ActionResult FromResult<T>(OperationResult<T> result)
    {
        if (result.Succeeded)
        {
            return Ok(result.Value);
        }

        return Problem(title: result.ErrorCode, detail: result.ErrorMessage, statusCode: result.StatusCode);
    }

    protected bool TryParseUtc(string? value, out DateTime? parsed, out ActionResult? error)
    {
        parsed = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            parsed = TimestampNormalizer.ParseUtc(value);
            return true;
        }
        catch (TimestampNormalizationException exception)
        {
            error = Problem(title: "InvalidTimestamp", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
            return false;
        }
    }
}
