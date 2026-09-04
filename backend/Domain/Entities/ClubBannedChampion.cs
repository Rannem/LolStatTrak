namespace LolStatTrak.Domain.Entities;

/// <summary>A champion banned from the randomizer for a specific club.</summary>
public class ClubBannedChampion
{
    public Guid ClubId { get; set; }
    public int ChampionId { get; set; }
}
