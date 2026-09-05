using LolStatTrak.Domain.Entities;

namespace LolStatTrak.Api.Auth;

/// <summary>The app JWT lives in an httpOnly cookie consumed by both REST calls and the SignalR handshake.</summary>
public static class SessionCookie
{
    public const string Name = "lst_session";

    public static void Issue(HttpResponse response, JwtTokenService tokens, User user)
    {
        response.Cookies.Append(Name, tokens.CreateToken(user), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        });
    }
}
