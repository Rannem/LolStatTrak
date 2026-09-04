namespace LolStatTrak.Api.Auth;

/// <summary>Symmetric-key JWT settings for the app's own session tokens (issued after Discord login).</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "LolStatTrak";
    public string Audience { get; set; } = "LolStatTrak";
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}
