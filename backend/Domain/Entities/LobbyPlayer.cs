namespace LolStatTrak.Domain.Entities;

public enum LobbyTeam
{
    Blue = 0,
    Red = 1,
}

/// <summary>A player's randomizer result within a lobby: assigned team and champion.</summary>
public class LobbyPlayer
{
    public Guid LobbyId { get; set; }
    public Guid UserId { get; set; }
    public LobbyTeam? AssignedTeam { get; set; }
    public int? AssignedChampionId { get; set; }
}
