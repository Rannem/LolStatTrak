using System.Security.Claims;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

public record CreateClubRequest(string Name);
public record JoinByInviteRequest(string InviteCode);
public record SetBannedChampionsRequest(int[] ChampionIds);

[ApiController]
[Route("api/clubs")]
[Authorize]
public class ClubsController(ClubRepository clubRepository, LobbyRepository lobbyRepository) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing user id claim"));

    [HttpGet]
    public async Task<IActionResult> GetMyClubs()
        => Ok(await clubRepository.GetForUserAsync(CurrentUserId));

    /// <summary>Club header info plus the caller's own role, so the UI can show/hide admin tools.</summary>
    [HttpGet("{clubId:guid}")]
    public async Task<IActionResult> Get(Guid clubId)
    {
        var membership = await clubRepository.GetMembershipAsync(clubId, CurrentUserId);
        if (membership is not { Status: ClubMembershipStatus.Approved })
            return Forbid();

        var club = await clubRepository.GetAsync(clubId);
        if (club is null)
            return NotFound();

        return Ok(new
        {
            club.Id,
            club.Name,
            club.Slug,
            club.OwnerUserId,
            club.InviteCode,
            club.CreatedAt,
            MyRole = membership.Role,
            CanManage = membership.Role is ClubMemberRole.Mod or ClubMemberRole.Admin or ClubMemberRole.Owner,
        });
    }

    [HttpGet("{clubId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid clubId)
    {
        if (!await IsApprovedMemberAsync(clubId))
            return Forbid();

        return Ok(await clubRepository.GetMembersAsync(clubId));
    }

    [HttpGet("{clubId:guid}/lobbies")]
    public async Task<IActionResult> GetLobbies(Guid clubId)
    {
        if (!await IsApprovedMemberAsync(clubId))
            return Forbid();

        return Ok(await lobbyRepository.GetForClubAsync(clubId));
    }

    /// <summary>Anyone can create a club; the creator becomes its Owner.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClubRequest request)
    {
        var slug = SlugGenerator.FromName(request.Name);
        var inviteCode = InviteCodeGenerator.Generate();
        var club = await clubRepository.CreateAsync(request.Name, slug, CurrentUserId, inviteCode);
        return Ok(club);
    }

    /// <summary>Auto-join flow: a valid invite code joins immediately as an Approved member.</summary>
    [HttpPost("join/invite")]
    public async Task<IActionResult> JoinByInvite([FromBody] JoinByInviteRequest request)
    {
        var club = await clubRepository.GetByInviteCodeAsync(request.InviteCode);
        if (club is null)
            return NotFound();

        await clubRepository.JoinViaInviteAsync(club.Id, CurrentUserId);
        return Ok(club);
    }

    /// <summary>Request-to-join flow: creates a Pending membership awaiting admin/mod approval.</summary>
    [HttpPost("{clubId:guid}/join-requests")]
    public async Task<IActionResult> RequestJoin(Guid clubId)
    {
        await clubRepository.RequestJoinAsync(clubId, CurrentUserId);
        return Ok();
    }

    [HttpGet("{clubId:guid}/join-requests")]
    public async Task<IActionResult> GetJoinRequests(Guid clubId)
    {
        if (!await IsAdminOrModAsync(clubId))
            return Forbid();

        return Ok(await clubRepository.GetPendingRequestsAsync(clubId));
    }

    [HttpPost("{clubId:guid}/join-requests/{userId:guid}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(Guid clubId, Guid userId)
    {
        if (!await IsAdminOrModAsync(clubId))
            return Forbid();

        await clubRepository.ApproveMemberAsync(clubId, userId);
        return Ok();
    }

    [HttpGet("{clubId:guid}/banned-champions")]
    public async Task<IActionResult> GetBannedChampions(Guid clubId)
        => Ok(await clubRepository.GetBannedChampionsAsync(clubId));

    [HttpPut("{clubId:guid}/banned-champions")]
    public async Task<IActionResult> SetBannedChampions(Guid clubId, [FromBody] SetBannedChampionsRequest request)
    {
        if (!await IsAdminOrModAsync(clubId))
            return Forbid();

        await clubRepository.SetBannedChampionsAsync(clubId, request.ChampionIds);
        return Ok();
    }

    private async Task<bool> IsApprovedMemberAsync(Guid clubId)
    {
        var membership = await clubRepository.GetMembershipAsync(clubId, CurrentUserId);
        return membership is { Status: ClubMembershipStatus.Approved };
    }

    private async Task<bool> IsAdminOrModAsync(Guid clubId)
    {
        var membership = await clubRepository.GetMembershipAsync(clubId, CurrentUserId);
        return membership is { Status: ClubMembershipStatus.Approved }
            && membership.Role is ClubMemberRole.Mod or ClubMemberRole.Admin or ClubMemberRole.Owner;
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
