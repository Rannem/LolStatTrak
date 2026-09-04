namespace LolStatTrak.Domain.Entities;

public enum ClubMemberRole
{
    Member = 0,
    Mod = 1,
    Admin = 2,
    Owner = 3,
}

public enum ClubMembershipStatus
{
    Pending = 0,
    Approved = 1,
}

/// <summary>Join-table linking a user to a club with a role and approval status.</summary>
public class ClubMember
{
    public Guid ClubId { get; set; }
    public Guid UserId { get; set; }
    public ClubMemberRole Role { get; set; } = ClubMemberRole.Member;
    public ClubMembershipStatus Status { get; set; } = ClubMembershipStatus.Pending;
    public DateTimeOffset JoinedAt { get; set; }
}
