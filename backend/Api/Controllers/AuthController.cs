using LolStatTrak.Api.Auth;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Repositories;
using LolStatTrak.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

public record LinkRiotAccountRequest(string GameName, string TagLine);

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserRepository userRepository,
    AuditRepository audit,
    JwtTokenService jwtTokenService,
    RiotApiClient riotApiClient,
    GlobalAdminOptions globalAdmins) : ControllerBase
{
    private const string SessionCookieName = SessionCookie.Name;

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

        var existing = await userRepository.GetByDiscordIdAsync(discordId);
        var user = await userRepository.UpsertFromDiscordAsync(discordId, username, avatarUrl, globalAdmins.Contains(discordId));
        if (existing is null)
            await audit.LogAsync(null, user.Id, "user.registered", "user", user.Id.ToString(), new { user.DiscordUsername });

        // Clear the temporary OAuth handshake cookie now that we have our own session.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        IssueSessionCookie(user);
        return Redirect(returnUrl ?? "/");
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(SessionCookieName);
        return Ok();
    }

    /// <summary>
    /// Returns the signed-in user's profile (including approval status so the SPA can show
    /// the "waiting for approval" screen); 401 if the session cookie is missing/expired.
    /// Re-issues the JWT only when the authorization claims actually changed (e.g. an approval
    /// granted since login), so the common case is a plain read with no Set-Cookie.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await userRepository.GetByIdAsync(User.GetUserId());
        if (user is null)
        {
            Response.Cookies.Delete(SessionCookieName);
            return Unauthorized();
        }

        var tokenSaysApproved = User.IsApproved();
        var tokenSaysGlobalAdmin = User.IsGlobalAdmin();
        var isApprovedNow = user.IsGlobalAdmin || user.AccessStatus == UserAccessStatus.Approved;
        if (tokenSaysApproved != isApprovedNow || tokenSaysGlobalAdmin != user.IsGlobalAdmin)
            IssueSessionCookie(user);

        Response.Headers.CacheControl = "no-store";
        return Ok(ToProfile(user));
    }

    /// <summary>
    /// Links a Riot ID (gameName#tagLine) to the signed-in user by resolving it to a PUUID via
    /// account-v1. This is what lets match tracking attribute stats to this player.
    /// </summary>
    [HttpPut("riot-account")]
    [Authorize(Policy = AppPolicies.Approved)]
    public async Task<IActionResult> LinkRiotAccount([FromBody] LinkRiotAccountRequest request, CancellationToken ct)
    {
        var gameName = request.GameName.Trim();
        var tagLine = request.TagLine.Trim().TrimStart('#');
        if (gameName.Length == 0 || tagLine.Length == 0)
            return BadRequest(new { title = "Enter both your game name and tag line (e.g. Faker#KR1)." });

        RiotAccount? account;
        try
        {
            account = await riotApiClient.ResolveAccountAsync(gameName, tagLine, ct);
        }
        catch (Exception)
        {
            return StatusCode(502, new { title = "Riot API is unavailable right now. Try again in a moment." });
        }

        if (account is null)
            return NotFound(new { title = $"No Riot account found for {gameName}#{tagLine}. Check the spelling and tag." });

        var userId = User.GetUserId();
        await userRepository.LinkRiotAccountAsync(userId, account.Puuid, account.GameName, account.TagLine);
        await audit.LogAsync(null, userId, "user.riot_linked", "user", userId.ToString(),
            new { account.GameName, account.TagLine });

        var user = await userRepository.GetByIdAsync(userId);
        return Ok(ToProfile(user!));
    }

    [HttpDelete("riot-account")]
    [Authorize(Policy = AppPolicies.Approved)]
    public async Task<IActionResult> UnlinkRiotAccount()
    {
        var userId = User.GetUserId();
        await userRepository.UnlinkRiotAccountAsync(userId);
        await audit.LogAsync(null, userId, "user.riot_unlinked", "user", userId.ToString());
        var user = await userRepository.GetByIdAsync(userId);
        return Ok(ToProfile(user!));
    }

    private void IssueSessionCookie(User user) => SessionCookie.Issue(Response, jwtTokenService, user);

    private static object ToProfile(User user) => new
    {
        user.Id,
        user.DiscordUsername,
        user.AvatarUrl,
        user.RiotGameName,
        user.RiotTagLine,
        RiotLinked = user.RiotPuuid is not null,
        user.IsGlobalAdmin,
        user.AccessStatus,
    };

    private const string ClaimTypesDiscordId = "urn:discord:id";
}
