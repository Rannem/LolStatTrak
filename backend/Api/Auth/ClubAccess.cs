using System.Security.Claims;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Repositories;

namespace LolStatTrak.Api.Auth;

/// <summary>
/// Central place for club-level permission checks. Global admins pass every check so they
/// can moderate any club without being a member of it.
/// </summary>
public class ClubAccess(ClubRepository clubRepository)
{
    // Scoped per request: several checks in one action shouldn't hit the DB more than once.
    private readonly Dictionary<(Guid ClubId, Guid UserId), ClubMember?> _memo = [];

    public async Task<bool> IsMemberAsync(ClaimsPrincipal user, Guid clubId)
        => user.IsGlobalAdmin() || await HasRoleAsync(user, clubId, ClubMemberRole.Member);

    /// <summary>Mods and above: bans, approving join requests.</summary>
    public async Task<bool> CanModerateAsync(ClaimsPrincipal user, Guid clubId)
        => user.IsGlobalAdmin() || await HasRoleAsync(user, clubId, ClubMemberRole.Mod);

    /// <summary>Admins and owner: deleting games, managing members, viewing the audit log.</summary>
    public async Task<bool> CanAdministerAsync(ClaimsPrincipal user, Guid clubId)
        => user.IsGlobalAdmin() || await HasRoleAsync(user, clubId, ClubMemberRole.Admin);

    /// <summary>Owner only (or global admin): promoting to Admin, deleting the club.</summary>
    public async Task<bool> IsOwnerAsync(ClaimsPrincipal user, Guid clubId)
        => user.IsGlobalAdmin() || await HasRoleAsync(user, clubId, ClubMemberRole.Owner);

    public async Task<ClubMember?> GetMembershipAsync(ClaimsPrincipal user, Guid clubId)
    {
        var key = (clubId, user.GetUserId());
        if (_memo.TryGetValue(key, out var cached))
            return cached;
        var membership = await clubRepository.GetMembershipAsync(clubId, key.Item2);
        _memo[key] = membership;
        return membership;
    }

    private async Task<bool> HasRoleAsync(ClaimsPrincipal user, Guid clubId, ClubMemberRole minimum)
    {
        var membership = await GetMembershipAsync(user, clubId);
        return membership is { Status: ClubMembershipStatus.Approved } && membership.Role >= minimum;
    }
}
