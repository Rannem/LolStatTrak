using LolStatTrak.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LolStatTrak.Api.Hubs;

/// <summary>Broadcasts live lobby presence and randomizer results to connected club members.</summary>
[Authorize(Policy = AppPolicies.Approved)]
public class LobbyHub : Hub
{
    public async Task JoinLobbyGroup(string lobbyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(lobbyId));
    }

    public async Task LeaveLobbyGroup(string lobbyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(lobbyId));
    }

    public static string GroupName(string lobbyId) => $"lobby:{lobbyId}";
}

/// <summary>Server-side events pushed to hub clients; keep in sync with the Angular hub client contract.</summary>
public static class LobbyHubEvents
{
    public const string PlayerJoined = "PlayerJoined";
    public const string LobbyRolled = "LobbyRolled";
}
