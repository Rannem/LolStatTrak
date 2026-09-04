namespace LolStatTrak.Domain.Entities;

public enum LobbyStatus
{
    Open = 0,
    Rolled = 1,
    Played = 2,
}

/// <summary>A custom-game lobby for a club, awaiting/holding a team+champion roll.</summary>
public class Lobby
{
    public Guid Id { get; set; }
    public Guid ClubId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public LobbyStatus Status { get; set; } = LobbyStatus.Open;
    public DateTimeOffset CreatedAt { get; set; }
}
