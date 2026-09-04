namespace LolStatTrak.Domain.Entities;

/// <summary>A player, authenticated via Discord OAuth2.</summary>
public class User
{
    public Guid Id { get; set; }
    public string DiscordId { get; set; } = string.Empty;
    public string DiscordUsername { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    // Riot account link, resolved once via account-v1 and reused for match polling.
    public string? RiotPuuid { get; set; }
    public string? RiotGameName { get; set; }
    public string? RiotTagLine { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
