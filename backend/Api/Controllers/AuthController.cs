using LolStatTrak.Api.Auth;
using LolStatTrak.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserRepository userRepository, JwtTokenService jwtTokenService) : ControllerBase
{
    /// <summary>Kicks off the Discord OAuth2 code flow.</summary>
    [HttpGet("discord/login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback), new { returnUrl }),
        };
        return Challenge(properties, "Discord");
    }

    /// <summary>
    /// Discord redirects here after the user approves. The Discord handler has already
    /// populated the temporary external cookie with the user's Discord profile claims;
    /// we upsert our own user row and issue our own JWT, then redirect back to the SPA.
    /// </summary>
    [HttpGet("discord/callback")]
    public async Task<IActionResult> Callback([FromQuery] string? returnUrl)
    {
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authResult.Succeeded || authResult.Principal is null)
            return Unauthorized();

        var discordId = authResult.Principal.FindFirst(ClaimTypesDiscordId)?.Value
            ?? authResult.Principal.FindFirst("sub")?.Value
            ?? string.Empty;
        var username = authResult.Principal.Identity?.Name ?? "Unknown";
        var avatarUrl = authResult.Principal.FindFirst("avatar_url")?.Value;

        var user = await userRepository.UpsertFromDiscordAsync(discordId, username, avatarUrl);
        var token = jwtTokenService.CreateToken(user);

        // App JWT stored as an httpOnly cookie, consumed by both REST calls and the SignalR handshake.
        Response.Cookies.Append("lst_session", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        });

        return Redirect(returnUrl ?? "/");
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("lst_session");
        return Ok();
    }

    private const string ClaimTypesDiscordId = "urn:discord:id";
}
