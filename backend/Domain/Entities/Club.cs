namespace LolStatTrak.Domain.Entities;

/// <summary>A friend group. Anyone can create one and becomes its owner.</summary>
public class Club
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }

    /// <summary>Shareable code that lets a user auto-join without approval.</summary>
    public string InviteCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
