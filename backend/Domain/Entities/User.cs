namespace LolStatTrak.Domain.Entities;

public enum UserAccessStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

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

    /// <summary>Site-wide superuser; synced from the GLOBAL_ADMIN_DISCORD_IDS env var at login.</summary>
    public bool IsGlobalAdmin { get; set; }

    /// <summary>New sign-ups start Pending and must be approved by a global admin before using the app.</summary>
    public UserAccessStatus AccessStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
