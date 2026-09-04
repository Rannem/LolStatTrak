using Dapper;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Data;

namespace LolStatTrak.Infrastructure.Repositories;

public class UserRepository(NpgsqlConnectionFactory connectionFactory)
{
    private const string Columns = """
        id "Id", discord_id "DiscordId", discord_username "DiscordUsername",
        avatar_url "AvatarUrl", riot_puuid "RiotPuuid", riot_game_name "RiotGameName",
        riot_tag_line "RiotTagLine", is_global_admin "IsGlobalAdmin", access_status "AccessStatus",
        created_at "CreatedAt"
        """;

    public async Task<User?> GetByIdAsync(Guid id)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"select {Columns} from users where id = @id", new { id });
    }

    public async Task<User?> GetByDiscordIdAsync(string discordId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"select {Columns} from users where discord_id = @discordId", new { discordId });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<User>($"select {Columns} from users order by created_at desc");
    }

    /// <summary>
    /// Creates or refreshes the user from their Discord profile. The global-admin flag is
    /// re-evaluated on every login so changing the env var takes effect at next sign-in;
    /// global admins are always auto-approved, everyone else starts Pending.
    /// </summary>
    public async Task<User> UpsertFromDiscordAsync(string discordId, string username, string? avatarUrl, bool isGlobalAdmin)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleAsync<User>(
            $"""
            insert into users (id, discord_id, discord_username, avatar_url, is_global_admin, access_status, created_at)
            values (gen_random_uuid(), @discordId, @username, @avatarUrl, @isGlobalAdmin, @initialStatus, now())
            on conflict (discord_id) do update
                set discord_username = excluded.discord_username,
                    avatar_url = excluded.avatar_url,
                    is_global_admin = excluded.is_global_admin,
                    access_status = case when excluded.is_global_admin then @approved else users.access_status end
            returning {Columns}
            """,
            new
            {
                discordId, username, avatarUrl, isGlobalAdmin,
                initialStatus = (int)(isGlobalAdmin ? UserAccessStatus.Approved : UserAccessStatus.Pending),
                approved = (int)UserAccessStatus.Approved,
            });
    }

    public async Task<IEnumerable<User>> GetByAccessStatusAsync(UserAccessStatus status)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<User>(
            $"select {Columns} from users where access_status = @status order by created_at",
            new { status = (int)status });
    }

    public async Task<bool> SetAccessStatusAsync(Guid userId, UserAccessStatus status)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteAsync(
            "update users set access_status = @status where id = @userId",
            new { userId, status = (int)status }) > 0;
    }

    public async Task<bool> DeleteAsync(Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteAsync("delete from users where id = @userId", new { userId }) > 0;
    }

    public async Task LinkRiotAccountAsync(Guid userId, string puuid, string gameName, string tagLine)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "update users set riot_puuid = @puuid, riot_game_name = @gameName, riot_tag_line = @tagLine where id = @userId",
            new { userId, puuid, gameName, tagLine });
    }

    public async Task UnlinkRiotAccountAsync(Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "update users set riot_puuid = null, riot_game_name = null, riot_tag_line = null where id = @userId",
            new { userId });
    }
}
