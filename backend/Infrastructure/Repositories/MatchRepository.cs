using Dapper;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Data;

namespace LolStatTrak.Infrastructure.Repositories;

public class MatchRepository(NpgsqlConnectionFactory connectionFactory)
{
    public async Task<bool> ExistsAsync(string riotMatchId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<bool>(
            "select exists(select 1 from matches where riot_match_id = @riotMatchId)",
            new { riotMatchId });
    }

    public async Task<Guid> InsertAsync(Match match, IEnumerable<MatchParticipant> participants)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var matchId = await conn.ExecuteScalarAsync<Guid>(
            """
            insert into matches (id, club_id, lobby_id, riot_match_id, played_at, queue_id, raw_payload)
            values (gen_random_uuid(), @ClubId, @LobbyId, @RiotMatchId, @PlayedAt, @QueueId, @RawPayload::jsonb)
            returning id
            """,
            match, tx);

        foreach (var p in participants)
        {
            p.MatchId = matchId;
            await conn.ExecuteAsync(
                """
                insert into match_participants
                    (id, match_id, user_id, puuid, champion_id, team, kills, deaths, assists, win, raw_stats)
                values
                    (gen_random_uuid(), @MatchId, @UserId, @Puuid, @ChampionId, @Team, @Kills, @Deaths, @Assists, @Win, @RawStats::jsonb)
                """,
                p, tx);
        }

        await tx.CommitAsync();
        return matchId;
    }

    public async Task<IEnumerable<MatchSummary>> ListForClubAsync(Guid clubId, int limit = 50)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        var matches = (await conn.QueryAsync<MatchSummary>(
            """
            select id "Id", club_id "ClubId", lobby_id "LobbyId", riot_match_id "RiotMatchId",
                   played_at "PlayedAt", queue_id "QueueId"
            from matches where club_id = @clubId
            order by played_at desc limit @limit
            """,
            new { clubId, limit })).ToList();

        if (matches.Count == 0)
            return matches;

        var participants = await conn.QueryAsync<MatchParticipantView>(
            """
            select p.match_id "MatchId", p.user_id "UserId", u.discord_username "DiscordUsername",
                   u.avatar_url "AvatarUrl", p.champion_id "ChampionId", p.team "Team",
                   p.kills "Kills", p.deaths "Deaths", p.assists "Assists", p.win "Win"
            from match_participants p
            join users u on u.id = p.user_id
            where p.match_id = any(@ids)
            order by p.team, u.discord_username
            """,
            new { ids = matches.Select(m => m.Id).ToArray() });

        var byMatch = participants.ToLookup(p => p.MatchId);
        foreach (var m in matches)
            m.Participants = byMatch[m.Id].ToList();

        return matches;
    }

    public async Task<MatchSummary?> GetAsync(Guid matchId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<MatchSummary>(
            """
            select id "Id", club_id "ClubId", lobby_id "LobbyId", riot_match_id "RiotMatchId",
                   played_at "PlayedAt", queue_id "QueueId"
            from matches where id = @matchId
            """,
            new { matchId });
    }

    /// <summary>Removes a match and (via FK cascade) its participant stat lines.</summary>
    public async Task<bool> DeleteAsync(Guid matchId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteAsync("delete from matches where id = @matchId", new { matchId }) > 0;
    }
}

public class MatchSummary
{
    public Guid Id { get; set; }
    public Guid ClubId { get; set; }
    public Guid? LobbyId { get; set; }
    public string RiotMatchId { get; set; } = string.Empty;
    public DateTimeOffset PlayedAt { get; set; }
    public int QueueId { get; set; }
    public List<MatchParticipantView> Participants { get; set; } = [];
}

public class MatchParticipantView
{
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public string DiscordUsername { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int ChampionId { get; set; }
    public LobbyTeam Team { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public bool Win { get; set; }
}
