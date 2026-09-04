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
}
