using LolStatTrak.Api.Auth;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

/// <summary>Site-wide administration: approving new sign-ups, overseeing all clubs, global audit.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = AppPolicies.GlobalAdmin)]
public class AdminController(
    UserRepository userRepository,
    ClubRepository clubRepository,
    AuditRepository audit) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    [HttpGet("users/pending")]
    public async Task<IActionResult> GetPendingUsers()
        => Ok((await userRepository.GetByAccessStatusAsync(UserAccessStatus.Pending)).Select(ToUserRow));

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
        => Ok((await userRepository.GetAllAsync()).Select(ToUserRow));

    [HttpPost("users/{userId:guid}/approve")]
    public Task<IActionResult> ApproveUser(Guid userId) => SetStatus(userId, UserAccessStatus.Approved, "user.approved");

    /// <summary>Rejected users keep a row (so they can't just re-register) but are locked out of the app.</summary>
    [HttpPost("users/{userId:guid}/reject")]
    public Task<IActionResult> RejectUser(Guid userId) => SetStatus(userId, UserAccessStatus.Rejected, "user.rejected");

    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        if (userId == CurrentUserId)
            return BadRequest(new { title = "You can't delete your own account from here." });

        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
            return NotFound();

        await audit.LogAsync(null, CurrentUserId, "user.deleted", "user", userId.ToString(), new { user.DiscordUsername });
        await userRepository.DeleteAsync(userId);
        return NoContent();
    }

    [HttpGet("clubs")]
    public async Task<IActionResult> GetClubs() => Ok(await clubRepository.GetAllAsync());

    [HttpDelete("clubs/{clubId:guid}")]
    public async Task<IActionResult> DeleteClub(Guid clubId)
    {
        var club = await clubRepository.GetAsync(clubId);
        if (club is null)
            return NotFound();

        await audit.LogAsync(null, CurrentUserId, "club.deleted", "club", clubId.ToString(), new { club.Name });
        await clubRepository.DeleteAsync(clubId);
        return NoContent();
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit() => Ok(await audit.ListAllAsync());

    private async Task<IActionResult> SetStatus(Guid userId, UserAccessStatus status, string action)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
            return NotFound();
        if (user.IsGlobalAdmin && status != UserAccessStatus.Approved)
            return BadRequest(new { title = "Global admins can't be rejected." });

        await userRepository.SetAccessStatusAsync(userId, status);
        await audit.LogAsync(null, CurrentUserId, action, "user", userId.ToString(), new { user.DiscordUsername });
        return Ok();
    }

    private static object ToUserRow(User u) => new
    {
        u.Id,
        u.DiscordUsername,
        u.AvatarUrl,
        u.RiotGameName,
        u.RiotTagLine,
        u.IsGlobalAdmin,
        u.AccessStatus,
        u.CreatedAt,
    };
}
