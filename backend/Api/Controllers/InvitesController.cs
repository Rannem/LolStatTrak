using LolStatTrak.Api.Auth;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

public record JoinByInviteRequest(string InviteCode);

/// <summary>
/// Invite-code redemption. Deliberately NOT behind the <see cref="AppPolicies.Approved"/> policy:
/// a club invite shared by an existing member is itself the trust signal, so redeeming one both
/// joins the club and approves a still-Pending account for site access. Global admins remain
/// the fallback for people who sign up without an invite. Rejected accounts stay locked out.
/// </summary>
[ApiController]
[Route("api/clubs/join")]
[Authorize]
public class InvitesController(
    ClubRepository clubRepository,
    UserRepository userRepository,
    AuditRepository audit,
    JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("invite")]
    public async Task<IActionResult> JoinByInvite([FromBody] JoinByInviteRequest request)
    {
        var userId = User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
            return Unauthorized();
        if (user.AccessStatus == UserAccessStatus.Rejected && !user.IsGlobalAdmin)
            return StatusCode(403, new { title = "Your account has been declined by an admin, so invites can't be used." });

        var club = await clubRepository.GetByInviteCodeAsync(request.InviteCode.Trim());
        if (club is null)
            return NotFound(new { title = "No club found with that invite code." });

        if (user.AccessStatus == UserAccessStatus.Pending)
        {
            await userRepository.SetAccessStatusAsync(userId, UserAccessStatus.Approved);
            await audit.LogAsync(null, userId, "user.approved_via_invite", "user", userId.ToString(), new { ClubId = club.Id, club.Name });
            user.AccessStatus = UserAccessStatus.Approved;
            // Refresh the JWT so the very next request already carries the Approved claim.
            SessionCookie.Issue(Response, jwtTokenService, user);
        }

        await clubRepository.JoinViaInviteAsync(club.Id, userId);
        await audit.LogAsync(club.Id, userId, "member.joined_via_invite", "user", userId.ToString());
        return Ok(club);
    }
}
