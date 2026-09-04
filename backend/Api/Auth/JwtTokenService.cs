using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LolStatTrak.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LolStatTrak.Api.Auth;

/// <summary>Issues the app's own session JWT after a successful Discord OAuth callback.</summary>
public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("discord_id", user.DiscordId),
            new(ClaimTypes.Name, user.DiscordUsername),
            new(AppClaims.AccessStatus, user.AccessStatus.ToString()),
        };
        if (user.IsGlobalAdmin)
            claims.Add(new Claim(AppClaims.GlobalAdmin, "true"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
