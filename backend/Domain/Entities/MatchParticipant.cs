namespace LolStatTrak.Domain.Entities;

/// <summary>One tracked player's stat line within a fetched match.</summary>
public class MatchParticipant
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public string Puuid { get; set; } = string.Empty;
    public int ChampionId { get; set; }
    public LobbyTeam Team { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public bool Win { get; set; }

    /// <summary>Full participant stat block from match-v5, kept as jsonb for flexibility.</summary>
    public string RawStats { get; set; } = string.Empty;
}
