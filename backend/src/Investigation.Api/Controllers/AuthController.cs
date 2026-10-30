using System.ComponentModel.DataAnnotations;
using Investigation.Api.Security;
using Investigation.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Investigation.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : PortalController
{
    private readonly AuthService _auth;
    private readonly TokenIssuer _tokens;
    private readonly JwtOptions _jwt;

    public AuthController(AuthService auth, TokenIssuer tokens, IOptions<JwtOptions> jwt)
    {
        _auth = auth;
        _tokens = tokens;
        _jwt = jwt.Value;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(request.Email, request.Password, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return FromResult(result);
        }

        return Ok(_tokens.Issue(result.Value, _jwt));
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult Me() => Ok(new
    {
        id = CurrentUserId,
        email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
        displayName = User.Identity?.Name,
        role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
    });
}

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
