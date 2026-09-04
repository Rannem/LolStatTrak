using Dapper;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Data;

namespace LolStatTrak.Infrastructure.Repositories;

/// <summary>Lobby player enriched with Discord display info for the UI.</summary>
public class LobbyPlayerView
{
    public Guid LobbyId { get; set; }
    public Guid UserId { get; set; }
    public string DiscordUsername { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public LobbyTeam? AssignedTeam { get; set; }
    public int? AssignedChampionId { get; set; }
}

public class LobbyRepository(NpgsqlConnectionFactory connectionFactory)
{
    public async Task<Lobby> CreateAsync(Guid clubId, Guid createdByUserId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleAsync<Lobby>(
            """
            insert into lobbies (id, club_id, created_by_user_id, status, created_at)
            values (gen_random_uuid(), @clubId, @createdByUserId, @status, now())
            returning id "Id", club_id "ClubId", created_by_user_id "CreatedByUserId",
                      status "Status", created_at "CreatedAt"
            """,
            new { clubId, createdByUserId, status = (int)LobbyStatus.Open });
    }

    public async Task<Lobby?> GetAsync(Guid lobbyId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Lobby>(
            """
            select id "Id", club_id "ClubId", created_by_user_id "CreatedByUserId",
                   status "Status", created_at "CreatedAt"
            from lobbies where id = @lobbyId
            """,
            new { lobbyId });
    }

    public async Task JoinAsync(Guid lobbyId, Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            insert into lobby_players (lobby_id, user_id) values (@lobbyId, @userId)
            on conflict (lobby_id, user_id) do nothing
            """,
            new { lobbyId, userId });
    }

    public async Task<IEnumerable<LobbyPlayer>> GetPlayersAsync(Guid lobbyId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<LobbyPlayer>(
            """
            select lobby_id "LobbyId", user_id "UserId", assigned_team "AssignedTeam",
                   assigned_champion_id "AssignedChampionId"
            from lobby_players where lobby_id = @lobbyId
            """,
            new { lobbyId });
    }

    /// <summary>Players joined with their Discord identity, for display in the lobby UI.</summary>
    public async Task<IEnumerable<LobbyPlayerView>> GetPlayerViewsAsync(Guid lobbyId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<LobbyPlayerView>(
            """
            select p.lobby_id "LobbyId", p.user_id "UserId", u.discord_username "DiscordUsername",
                   u.avatar_url "AvatarUrl", p.assigned_team "AssignedTeam",
                   p.assigned_champion_id "AssignedChampionId"
            from lobby_players p
            join users u on u.id = p.user_id
            where p.lobby_id = @lobbyId
            order by p.assigned_team nulls last, u.discord_username
            """,
            new { lobbyId });
    }

    public async Task<IEnumerable<Lobby>> GetForClubAsync(Guid clubId, int limit = 20)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<Lobby>(
            """
            select id "Id", club_id "ClubId", created_by_user_id "CreatedByUserId",
                   status "Status", created_at "CreatedAt"
            from lobbies where club_id = @clubId
            order by created_at desc
            limit @limit
            """,
            new { clubId, limit });
    }

    public async Task SaveRollAsync(Guid lobbyId, IEnumerable<LobbyPlayer> players)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var p in players)
        {
            await conn.ExecuteAsync(
                """
                update lobby_players set assigned_team = @AssignedTeam, assigned_champion_id = @AssignedChampionId
                where lobby_id = @LobbyId and user_id = @UserId
                """,
                p, tx);
        }
        await conn.ExecuteAsync(
            "update lobbies set status = @status where id = @lobbyId",
            new { lobbyId, status = (int)LobbyStatus.Rolled }, tx);
        await tx.CommitAsync();
    }
}
