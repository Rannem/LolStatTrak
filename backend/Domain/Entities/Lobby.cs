namespace LolStatTrak.Domain.Entities;

public enum LobbyStatus
{
    Open = 0,
    Rolled = 1,
    Played = 2,
}

/// <summary>Which custom game the lobby is for. Drives what the randomizer assigns.</summary>
public enum LobbyGameMode
{
    /// <summary>Classic ARAM custom (blind pick) — we roll teams and champions.</summary>
    Aram = 0,
    /// <summary>ARAM Mayhem — the client forces all-random, so we only roll teams.</summary>
    AramMayhem = 1,
    /// <summary>5v5 Summoner's Rift custom — teams, optionally champions.</summary>
    SummonersRift = 2,
}

/// <summary>A custom-game lobby for a club, awaiting/holding a team (+ optional champion) roll.</summary>
public class Lobby
{
    public Guid Id { get; set; }
    public Guid ClubId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public LobbyStatus Status { get; set; } = LobbyStatus.Open;
    public LobbyGameMode GameMode { get; set; } = LobbyGameMode.Aram;
    public bool AssignChampions { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
