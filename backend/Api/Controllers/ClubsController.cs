using LolStatTrak.Api.Auth;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

public record CreateClubRequest(string Name);
public record JoinByInviteRequest(string InviteCode);
public record SetBannedChampionsRequest(int[] ChampionIds);
public record SetMemberRoleRequest(ClubMemberRole Role);

[ApiController]
[Route("api/clubs")]
[Authorize(Policy = AppPolicies.Approved)]
public class ClubsController(
    ClubRepository clubRepository,
    LobbyRepository lobbyRepository,
    MatchRepository matchRepository,
    AuditRepository audit,
    ClubAccess access) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> GetMyClubs()
        => Ok(await clubRepository.GetForUserAsync(CurrentUserId));

    /// <summary>Anyone can create a club; the creator becomes its Owner.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClubRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length is 0 or > 64)
            return BadRequest(new { title = "Club name must be 1–64 characters." });

        var club = await clubRepository.CreateAsync(name, SlugGenerator.FromName(name), CurrentUserId, InviteCodeGenerator.Generate());
        await audit.LogAsync(club.Id, CurrentUserId, "club.created", "club", club.Id.ToString(), new { club.Name });
        return Ok(club);
    }

    /// <summary>Club header info plus the caller's own role, so the UI can show/hide admin tools.</summary>
    [HttpGet("{clubId:guid}")]
    public async Task<IActionResult> Get(Guid clubId)
    {
        if (!await access.IsMemberAsync(User, clubId))
            return Forbid();

        var club = await clubRepository.GetAsync(clubId);
        if (club is null)
            return NotFound();

        var membership = await access.GetMembershipAsync(User, clubId);
        var role = membership?.Role;
        var isGlobalAdmin = User.IsGlobalAdmin();

        return Ok(new
        {
            club.Id,
            club.Name,
            club.Slug,
            club.OwnerUserId,
            club.InviteCode,
            club.CreatedAt,
            MyRole = role,
            IsGlobalAdmin = isGlobalAdmin,
            CanManage = isGlobalAdmin || role >= ClubMemberRole.Mod,
            CanAdminister = isGlobalAdmin || role >= ClubMemberRole.Admin,
            IsOwner = isGlobalAdmin || role == ClubMemberRole.Owner,
        });
    }

    /// <summary>Owner (or global admin) deletes the club and everything in it.</summary>
    [HttpDelete("{clubId:guid}")]
    public async Task<IActionResult> Delete(Guid clubId)
    {
        if (!await access.IsOwnerAsync(User, clubId))
            return Forbid();

        var club = await clubRepository.GetAsync(clubId);
        if (club is null)
            return NotFound();

        // Log to the global stream (no club id) since the club row is about to disappear.
        await audit.LogAsync(null, CurrentUserId, "club.deleted", "club", clubId.ToString(), new { club.Name });
        await clubRepository.DeleteAsync(clubId);
        return NoContent();
    }

    // --- Membership ----------------------------------------------------------------------

    /// <summary>Auto-join flow: a valid invite code joins immediately as an Approved member.</summary>
    [HttpPost("join/invite")]
    public async Task<IActionResult> JoinByInvite([FromBody] JoinByInviteRequest request)
    {
        var club = await clubRepository.GetByInviteCodeAsync(request.InviteCode.Trim());
        if (club is null)
            return NotFound(new { title = "No club found with that invite code." });

        await clubRepository.JoinViaInviteAsync(club.Id, CurrentUserId);
        await audit.LogAsync(club.Id, CurrentUserId, "member.joined_via_invite", "user", CurrentUserId.ToString());
        return Ok(club);
    }

    /// <summary>Request-to-join flow: creates a Pending membership awaiting mod approval.</summary>
    [HttpPost("{clubId:guid}/join-requests")]
    public async Task<IActionResult> RequestJoin(Guid clubId)
    {
        await clubRepository.RequestJoinAsync(clubId, CurrentUserId);
        await audit.LogAsync(clubId, CurrentUserId, "member.join_requested", "user", CurrentUserId.ToString());
        return Ok();
    }

    [HttpGet("{clubId:guid}/join-requests")]
    public async Task<IActionResult> GetJoinRequests(Guid clubId)
    {
        if (!await access.CanModerateAsync(User, clubId))
            return Forbid();

        return Ok(await clubRepository.GetPendingRequestsAsync(clubId));
    }

    [HttpPost("{clubId:guid}/join-requests/{userId:guid}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(Guid clubId, Guid userId)
    {
        if (!await access.CanModerateAsync(User, clubId))
            return Forbid();

        await clubRepository.ApproveMemberAsync(clubId, userId);
        await audit.LogAsync(clubId, CurrentUserId, "member.approved", "user", userId.ToString());
        return Ok();
    }

    [HttpGet("{clubId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid clubId)
    {
        if (!await access.IsMemberAsync(User, clubId))
            return Forbid();

        return Ok(await clubRepository.GetMembersAsync(clubId));
    }

    /// <summary>
    /// Admins can promote/demote between Member and Mod; only the Owner (or a global admin)
    /// can grant/revoke Admin. Nobody can change the Owner's role here.
    /// </summary>
    [HttpPut("{clubId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> SetMemberRole(Guid clubId, Guid userId, [FromBody] SetMemberRoleRequest request)
    {
        if (request.Role == ClubMemberRole.Owner)
            return BadRequest(new { title = "Ownership can't be transferred this way." });

        var needsOwner = request.Role == ClubMemberRole.Admin
            || (await clubRepository.GetMembershipAsync(clubId, userId))?.Role == ClubMemberRole.Admin;
        var allowed = needsOwner ? await access.IsOwnerAsync(User, clubId) : await access.CanAdministerAsync(User, clubId);
        if (!allowed)
            return Forbid();

        if (!await clubRepository.SetMemberRoleAsync(clubId, userId, request.Role))
            return NotFound();

        await audit.LogAsync(clubId, CurrentUserId, "member.role_changed", "user", userId.ToString(), new { request.Role });
        return Ok();
    }

    [HttpDelete("{clubId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid clubId, Guid userId)
    {
        var isSelf = userId == CurrentUserId;
        if (!isSelf && !await access.CanAdministerAsync(User, clubId))
            return Forbid();

        if (!await clubRepository.RemoveMemberAsync(clubId, userId))
            return NotFound(new { title = "Member not found, or is the owner." });

        await audit.LogAsync(clubId, CurrentUserId, isSelf ? "member.left" : "member.removed", "user", userId.ToString());
        return NoContent();
    }

    // --- Bans ------------------------------------------------------------------------------

    [HttpGet("{clubId:guid}/banned-champions")]
    public async Task<IActionResult> GetBannedChampions(Guid clubId)
    {
        if (!await access.IsMemberAsync(User, clubId))
            return Forbid();

        return Ok(await clubRepository.GetBannedChampionsAsync(clubId));
    }

    [HttpPut("{clubId:guid}/banned-champions")]
    public async Task<IActionResult> SetBannedChampions(Guid clubId, [FromBody] SetBannedChampionsRequest request)
    {
        if (!await access.CanModerateAsync(User, clubId))
            return Forbid();

        var ids = request.ChampionIds.Distinct().ToArray();
        await clubRepository.SetBannedChampionsAsync(clubId, ids);
        await audit.LogAsync(clubId, CurrentUserId, "bans.updated", "club", clubId.ToString(), new { Count = ids.Length, ChampionIds = ids });
        return Ok();
    }

    // --- Lobbies & matches -----------------------------------------------------------------

    [HttpGet("{clubId:guid}/lobbies")]
    public async Task<IActionResult> GetLobbies(Guid clubId)
    {
        if (!await access.IsMemberAsync(User, clubId))
            return Forbid();

        return Ok(await lobbyRepository.GetForClubAsync(clubId));
    }

    [HttpDelete("{clubId:guid}/lobbies/{lobbyId:guid}")]
    public async Task<IActionResult> DeleteLobby(Guid clubId, Guid lobbyId)
    {
        if (!await access.CanAdministerAsync(User, clubId))
            return Forbid();

        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null || lobby.ClubId != clubId)
            return NotFound();

        await lobbyRepository.DeleteAsync(lobbyId);
        await audit.LogAsync(clubId, CurrentUserId, "lobby.deleted", "lobby", lobbyId.ToString(), new { lobby.Status, lobby.CreatedAt });
        return NoContent();
    }

    [HttpGet("{clubId:guid}/matches")]
    public async Task<IActionResult> GetMatches(Guid clubId)
    {
        if (!await access.IsMemberAsync(User, clubId))
            return Forbid();

        return Ok(await matchRepository.ListForClubAsync(clubId));
    }

    /// <summary>Removes a tracked game (and its stat lines) — e.g. a mis-correlated or remade match.</summary>
    [HttpDelete("{clubId:guid}/matches/{matchId:guid}")]
    public async Task<IActionResult> DeleteMatch(Guid clubId, Guid matchId)
    {
        if (!await access.CanAdministerAsync(User, clubId))
            return Forbid();

        var match = await matchRepository.GetAsync(matchId);
        if (match is null || match.ClubId != clubId)
            return NotFound();

        await matchRepository.DeleteAsync(matchId);
        await audit.LogAsync(clubId, CurrentUserId, "match.deleted", "match", matchId.ToString(), new { match.RiotMatchId, match.PlayedAt });
        return NoContent();
    }

    // --- Audit -----------------------------------------------------------------------------

    [HttpGet("{clubId:guid}/audit")]
    public async Task<IActionResult> GetAudit(Guid clubId)
    {
        if (!await access.CanAdministerAsync(User, clubId))
            return Forbid();

        return Ok(await audit.ListForClubAsync(clubId));
    }
}

internal static class SlugGenerator
{
    public static string FromName(string name)
    {
        var slug = name.Trim().ToLowerInvariant().Replace(' ', '-');
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{slug}-{suffix}";
    }
}

internal static class InviteCodeGenerator
{
    public static string Generate() => Guid.NewGuid().ToString("N")[..10];
}
