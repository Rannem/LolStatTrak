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

[ApiController]
[Route("api/lobbies")]
[Authorize]
public class LobbiesController(
    LobbyRepository lobbyRepository,
    ClubRepository clubRepository,
    RandomizerService randomizerService,
    LobbyMatchCorrelationService correlationService,
    ChampionCatalogService championCatalog,
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

    /// <summary>Current lobby state (metadata + players) for the initial page load.</summary>
    [HttpGet("{lobbyId:guid}")]
    public async Task<IActionResult> Get(Guid lobbyId)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();

        var players = await lobbyRepository.GetPlayerViewsAsync(lobbyId);
        return Ok(new { lobby, players });
    }

    [HttpPost("{lobbyId:guid}/join")]
    public async Task<IActionResult> Join(Guid lobbyId)
    {
        await lobbyRepository.JoinAsync(lobbyId, CurrentUserId);
        var players = (await lobbyRepository.GetPlayerViewsAsync(lobbyId)).ToList();
        await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
            .SendAsync(LobbyHubEvents.PlayerJoined, new { lobbyId, userId = CurrentUserId, players });
        return Ok(players);
    }

    /// <summary>Rolls random teams + champions for everyone currently in the lobby, honoring the club's bans.</summary>
    [HttpPost("{lobbyId:guid}/roll")]
    public async Task<IActionResult> Roll(Guid lobbyId, CancellationToken ct)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();

        var players = (await lobbyRepository.GetPlayersAsync(lobbyId)).Select(p => p.UserId).ToList();
        var bannedChampions = await clubRepository.GetBannedChampionsAsync(lobby.ClubId);
        var catalog = await championCatalog.GetAsync(ct);

        var rolled = randomizerService.Roll(lobbyId, players, catalog.AllIds, bannedChampions);
        await lobbyRepository.SaveRollAsync(lobbyId, rolled);

        var views = (await lobbyRepository.GetPlayerViewsAsync(lobbyId)).ToList();
        await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
            .SendAsync(LobbyHubEvents.LobbyRolled, views, ct);

        return Ok(views);
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
