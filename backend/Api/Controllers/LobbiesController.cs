using System.Security.Claims;
using LolStatTrak.Api.Hubs;
using LolStatTrak.Domain.Services;
using LolStatTrak.Infrastructure.Repositories;
using LolStatTrak.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace LolStatTrak.Api.Controllers;

public record CreateLobbyRequest(Guid ClubId);

/// <summary>Full LoL champion id pool used by the randomizer, minus each club's bans.</summary>
public static class ChampionCatalog
{
    // Kept intentionally minimal here; in production this should be refreshed from Riot's
    // Data Dragon champion.json so new releases show up automatically.
    public static readonly IReadOnlyCollection<int> AllChampionIds = Enumerable.Range(1, 170).ToList();
}

[ApiController]
[Route("api/lobbies")]
[Authorize]
public class LobbiesController(
    LobbyRepository lobbyRepository,
    ClubRepository clubRepository,
    RandomizerService randomizerService,
    LobbyMatchCorrelationService correlationService,
    IHubContext<LobbyHub> hubContext) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing user id claim"));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLobbyRequest request)
    {
        var lobby = await lobbyRepository.CreateAsync(request.ClubId, CurrentUserId);
        await lobbyRepository.JoinAsync(lobby.Id, CurrentUserId);
        return Ok(lobby);
    }

    [HttpPost("{lobbyId:guid}/join")]
    public async Task<IActionResult> Join(Guid lobbyId)
    {
        await lobbyRepository.JoinAsync(lobbyId, CurrentUserId);
        var players = await lobbyRepository.GetPlayersAsync(lobbyId);
        await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
            .SendAsync(LobbyHubEvents.PlayerJoined, new { lobbyId, userId = CurrentUserId });
        return Ok(players);
    }

    /// <summary>Rolls random teams + champions for everyone currently in the lobby, honoring the club's bans.</summary>
    [HttpPost("{lobbyId:guid}/roll")]
    public async Task<IActionResult> Roll(Guid lobbyId)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();

        var players = (await lobbyRepository.GetPlayersAsync(lobbyId)).Select(p => p.UserId).ToList();
        var bannedChampions = await clubRepository.GetBannedChampionsAsync(lobby.ClubId);

        var rolled = randomizerService.Roll(lobbyId, players, ChampionCatalog.AllChampionIds, bannedChampions);
        await lobbyRepository.SaveRollAsync(lobbyId, rolled);

        await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
            .SendAsync(LobbyHubEvents.LobbyRolled, rolled);

        return Ok(rolled);
    }

    /// <summary>
    /// Marks the lobby as played and kicks off a best-effort Riot match-v5 lookup to attach
    /// real stats. Safe to call even if some/all players haven't linked a Riot account yet.
    /// </summary>
    [HttpPost("{lobbyId:guid}/mark-played")]
    public async Task<IActionResult> MarkPlayed(Guid lobbyId, CancellationToken ct)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();

        var matchId = await correlationService.TryCorrelateAndPersistAsync(lobbyId, lobby.ClubId, lobby.CreatedAt, ct);
        return Ok(new { matchId });
    }
}
