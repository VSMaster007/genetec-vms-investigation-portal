using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Investigation.Application;
using Microsoft.IdentityModel.Tokens;

namespace Investigation.Api.Security;

public sealed class TokenIssuer
{
    public LoginResultDto Issue(AuthenticatedUser user, JwtOptions options)
    {
        var expires = DateTime.UtcNow.AddMinutes(options.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
                SecurityAlgorithms.HmacSha256));
        return new LoginResultDto(new JwtSecurityTokenHandler().WriteToken(token), expires, user);
    }
}
