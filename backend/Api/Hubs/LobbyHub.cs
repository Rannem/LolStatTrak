using LolStatTrak.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LolStatTrak.Api.Hubs;

/// <summary>Broadcasts live lobby presence and randomizer results to connected club members.</summary>
[Authorize(Policy = AppPolicies.Approved)]
public class LobbyHub(ClubAccess access) : Hub
{
    public async Task JoinLobbyGroup(string lobbyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(lobbyId));
    }

    public async Task LeaveLobbyGroup(string lobbyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(lobbyId));
    }

    /// <summary>Club-wide feed (new/changed/deleted lobbies). Only members may subscribe.</summary>
    public async Task JoinClubGroup(string clubId)
    {
        if (Context.User is null || !Guid.TryParse(clubId, out var id) || !await access.IsMemberAsync(Context.User, id))
            throw new HubException("Not a member of this club.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ClubGroupName(clubId));
    }

    public async Task LeaveClubGroup(string clubId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ClubGroupName(clubId));
    }

    public static string GroupName(string lobbyId) => $"lobby:{lobbyId}";
    public static string ClubGroupName(string clubId) => $"club:{clubId}";
}

/// <summary>Server-side events pushed to hub clients; keep in sync with the Angular hub client contract.</summary>
public static class LobbyHubEvents
{
    public const string PlayerJoined = "PlayerJoined";
    public const string LobbyRolled = "LobbyRolled";
    public const string LobbyPlayed = "LobbyPlayed";
    /// <summary>Sent to the club group with the full Lobby whenever one is created or changes status.</summary>
    public const string ClubLobbyUpserted = "ClubLobbyUpserted";
    public const string ClubLobbyDeleted = "ClubLobbyDeleted";
}
