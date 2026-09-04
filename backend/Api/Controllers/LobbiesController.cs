using LolStatTrak.Api.Auth;
using LolStatTrak.Api.Hubs;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Domain.Services;
using LolStatTrak.Infrastructure.Repositories;
using LolStatTrak.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace LolStatTrak.Api.Controllers;

public record CreateLobbyRequest(Guid ClubId, LobbyGameMode GameMode = LobbyGameMode.Aram, bool? AssignChampions = null);

[ApiController]
[Route("api/lobbies")]
[Authorize(Policy = AppPolicies.Approved)]
public class LobbiesController(
    LobbyRepository lobbyRepository,
    ClubRepository clubRepository,
    MatchRepository matchRepository,
    AuditRepository audit,
    ClubAccess access,
    RandomizerService randomizerService,
    LobbyMatchCorrelationService correlationService,
    ChampionCatalogService championCatalog,
    IHubContext<LobbyHub> hubContext) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLobbyRequest request)
    {
        if (!await access.IsMemberAsync(User, request.ClubId))
            return Forbid();

        // ARAM Mayhem can't be blind pick in the client, so the app never assigns champions for it.
        var assignChampions = request.GameMode != LobbyGameMode.AramMayhem && (request.AssignChampions ?? true);

        var lobby = await lobbyRepository.CreateAsync(request.ClubId, CurrentUserId, request.GameMode, assignChampions);
        await lobbyRepository.JoinAsync(lobby.Id, CurrentUserId);
        await audit.LogAsync(request.ClubId, CurrentUserId, "lobby.created", "lobby", lobby.Id.ToString(),
            new { GameMode = request.GameMode.ToString(), AssignChampions = assignChampions });
        return Ok(lobby);
    }

    /// <summary>Current lobby state (metadata + players) for the initial page load.</summary>
    [HttpGet("{lobbyId:guid}")]
    public async Task<IActionResult> Get(Guid lobbyId)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();
        if (!await access.IsMemberAsync(User, lobby.ClubId))
            return Forbid();

        var players = await lobbyRepository.GetPlayerViewsAsync(lobbyId);
        var matchId = lobby.Status == LobbyStatus.Played ? await matchRepository.GetIdForLobbyAsync(lobbyId) : null;
        return Ok(new { lobby, players, matchId });
    }

    [HttpPost("{lobbyId:guid}/join")]
    public async Task<IActionResult> Join(Guid lobbyId)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();
        if (!await access.IsMemberAsync(User, lobby.ClubId))
            return Forbid();

        await lobbyRepository.JoinAsync(lobbyId, CurrentUserId);
        var players = (await lobbyRepository.GetPlayerViewsAsync(lobbyId)).ToList();
        await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
            .SendAsync(LobbyHubEvents.PlayerJoined, new { lobbyId, userId = CurrentUserId, players });
        return Ok(players);
    }

    /// <summary>Rolls random teams (and champions, unless the lobby is teams-only) for everyone in the lobby, honoring the club's bans.</summary>
    [HttpPost("{lobbyId:guid}/roll")]
    public async Task<IActionResult> Roll(Guid lobbyId, CancellationToken ct)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();
        if (!await access.IsMemberAsync(User, lobby.ClubId))
            return Forbid();

        var players = (await lobbyRepository.GetPlayersAsync(lobbyId)).Select(p => p.UserId).ToList();
        if (players.Count == 0)
            return BadRequest(new { title = "Nobody has joined the lobby yet." });

        var bannedChampions = await clubRepository.GetBannedChampionsAsync(lobby.ClubId);
        var catalog = await championCatalog.GetAsync(ct);

        var rolled = randomizerService.Roll(lobbyId, players, catalog.AllIds, bannedChampions, lobby.AssignChampions);
        await lobbyRepository.SaveRollAsync(lobbyId, rolled);
        await audit.LogAsync(lobby.ClubId, CurrentUserId, "lobby.rolled", "lobby", lobbyId.ToString(),
            new { Players = players.Count, lobby.AssignChampions });

        var views = (await lobbyRepository.GetPlayerViewsAsync(lobbyId)).ToList();
        await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
            .SendAsync(LobbyHubEvents.LobbyRolled, views, ct);

        return Ok(views);
    }

    /// <summary>
    /// Marks the lobby as played and kicks off a best-effort Riot match-v5 lookup to attach
    /// real stats. Safe to call even if some/all players haven't linked a Riot account yet;
    /// if the game isn't in Riot's history yet, use <c>sync-stats</c> to retry later.
    /// </summary>
    [HttpPost("{lobbyId:guid}/mark-played")]
    public async Task<IActionResult> MarkPlayed(Guid lobbyId, CancellationToken ct)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();
        if (!await access.IsMemberAsync(User, lobby.ClubId))
            return Forbid();

        if (lobby.Status != LobbyStatus.Played)
            await lobbyRepository.SetStatusAsync(lobbyId, LobbyStatus.Played);

        var result = await correlationService.TryCorrelateAndPersistAsync(lobbyId, lobby.ClubId, lobby.CreatedAt, ct);
        await audit.LogAsync(lobby.ClubId, CurrentUserId, "lobby.marked_played", "lobby", lobbyId.ToString(),
            new { Outcome = result.Outcome.ToString(), result.MatchId, result.LinkedPlayers, result.TotalPlayers });

        await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
            .SendAsync(LobbyHubEvents.LobbyPlayed, new { lobbyId, result }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Re-runs the Riot match lookup for a lobby that has already been marked played. Riot
    /// typically needs a few minutes after the game ends before it shows up in match history.
    /// </summary>
    [HttpPost("{lobbyId:guid}/sync-stats")]
    public async Task<IActionResult> SyncStats(Guid lobbyId, CancellationToken ct)
    {
        var lobby = await lobbyRepository.GetAsync(lobbyId);
        if (lobby is null)
            return NotFound();
        if (!await access.IsMemberAsync(User, lobby.ClubId))
            return Forbid();
        if (lobby.Status != LobbyStatus.Played)
            return BadRequest(new { title = "Mark the lobby as played first." });

        var result = await correlationService.TryCorrelateAndPersistAsync(lobbyId, lobby.ClubId, lobby.CreatedAt, ct);
        if (result.Outcome == CorrelationOutcome.Found)
        {
            await audit.LogAsync(lobby.ClubId, CurrentUserId, "lobby.stats_synced", "lobby", lobbyId.ToString(), new { result.MatchId });
            await hubContext.Clients.Group(LobbyHub.GroupName(lobbyId.ToString()))
                .SendAsync(LobbyHubEvents.LobbyPlayed, new { lobbyId, result }, ct);
        }
        return Ok(result);
    }
}
