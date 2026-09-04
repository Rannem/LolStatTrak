namespace LolStatTrak.Domain.Entities;

/// <summary>A completed Riot match, fetched via match-v5 and correlated to a lobby.</summary>
public class Match
{
    public Guid Id { get; set; }
    public Guid ClubId { get; set; }
    public Guid? LobbyId { get; set; }
    public string RiotMatchId { get; set; } = string.Empty;
    public DateTimeOffset PlayedAt { get; set; }
    public int QueueId { get; set; }

    /// <summary>Full match-v5 response payload, kept for flexibility/future stat additions.</summary>
    public string RawPayload { get; set; } = string.Empty;
}
