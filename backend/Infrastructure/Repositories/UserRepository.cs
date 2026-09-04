using Dapper;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Data;

namespace LolStatTrak.Infrastructure.Repositories;

public class UserRepository(NpgsqlConnectionFactory connectionFactory)
{
    public async Task<User?> GetByIdAsync(Guid id)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            """
            select id "Id", discord_id "DiscordId", discord_username "DiscordUsername",
                   avatar_url "AvatarUrl", riot_puuid "RiotPuuid", riot_game_name "RiotGameName",
                   riot_tag_line "RiotTagLine", created_at "CreatedAt"
            from users where id = @id
            """,
            new { id });
    }

    public async Task<User?> GetByDiscordIdAsync(string discordId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            """
            select id "Id", discord_id "DiscordId", discord_username "DiscordUsername",
                   avatar_url "AvatarUrl", riot_puuid "RiotPuuid", riot_game_name "RiotGameName",
                   riot_tag_line "RiotTagLine", created_at "CreatedAt"
            from users where discord_id = @discordId
            """,
            new { discordId });
    }

    public async Task<User> UpsertFromDiscordAsync(string discordId, string username, string? avatarUrl)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        var user = await conn.QuerySingleAsync<User>(
            """
            insert into users (id, discord_id, discord_username, avatar_url, created_at)
            values (gen_random_uuid(), @discordId, @username, @avatarUrl, now())
            on conflict (discord_id) do update
                set discord_username = excluded.discord_username, avatar_url = excluded.avatar_url
            returning id "Id", discord_id "DiscordId", discord_username "DiscordUsername",
                      avatar_url "AvatarUrl", riot_puuid "RiotPuuid", riot_game_name "RiotGameName",
                      riot_tag_line "RiotTagLine", created_at "CreatedAt"
            """,
            new { discordId, username, avatarUrl });
        return user;
    }

    public async Task LinkRiotAccountAsync(Guid userId, string puuid, string gameName, string tagLine)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "update users set riot_puuid = @puuid, riot_game_name = @gameName, riot_tag_line = @tagLine where id = @userId",
            new { userId, puuid, gameName, tagLine });
    }
}
