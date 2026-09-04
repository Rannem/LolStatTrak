namespace LolStatTrak.Infrastructure.Options;

/// <summary>
/// Riot API configuration. Targets a *Personal* API key (registered once via the Riot
/// Developer Portal for private-community use) — not the 24h dev key, not a Production key.
/// See plan notes: personal keys are explicitly allowed for small private-community projects
/// and do not require daily rotation, unlike the default development key.
/// </summary>
public class RiotApiOptions
{
    public const string SectionName = "RiotApi";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Regional routing value for match-v5/account-v1 (e.g. "europe", "americas", "asia").</summary>
    public string RegionalRouting { get; set; } = "europe";
}
