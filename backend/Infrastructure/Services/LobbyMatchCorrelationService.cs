using System.Text.Json;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Data;
using LolStatTrak.Infrastructure.Repositories;
using Dapper;

namespace LolStatTrak.Infrastructure.Services;

/// <summary>
/// Correlates a played lobby with a finished Riot match: pulls each registered player's
/// recent match ids, fetches the raw payload, and matches on participant-PUUID overlap with
/// a match played after the lobby was created. Persists the match + tracked participants.
/// </summary>
public class LobbyMatchCorrelationService(
    RiotApiClient riotClient,
    MatchRepository matchRepository,
    NpgsqlConnectionFactory connectionFactory)
{
    public async Task<Guid?> TryCorrelateAndPersistAsync(Guid lobbyId, Guid clubId, DateTimeOffset lobbyCreatedAt, CancellationToken ct = default)
    {
        var trackedPlayers = await GetTrackedPlayersAsync(lobbyId, ct);
        if (trackedPlayers.Count == 0)
            return null;

        var trackedPuuids = trackedPlayers.Select(p => p.Puuid).ToHashSet();

        foreach (var player in trackedPlayers)
        {
            var matchIds = await riotClient.GetRecentMatchIdsAsync(player.Puuid, count: 5, ct);
            foreach (var matchId in matchIds)
            {
                if (await matchRepository.ExistsAsync(matchId))
                    continue;

                var rawJson = await riotClient.GetMatchRawJsonAsync(matchId, ct);
                if (rawJson is null)
                    continue;

                using var doc = JsonDocument.Parse(rawJson);
                var info = doc.RootElement.GetProperty("info");
                var gameEndMs = info.GetProperty("gameEndTimestamp").GetInt64();
                var playedAt = DateTimeOffset.FromUnixTimeMilliseconds(gameEndMs);
                if (playedAt < lobbyCreatedAt)
                    continue;

                var queueId = info.GetProperty("queueId").GetInt32();
                var participantsJson = info.GetProperty("participants");

                var participants = new List<MatchParticipant>();
                foreach (var participant in participantsJson.EnumerateArray())
                {
                    var puuid = participant.GetProperty("puuid").GetString() ?? string.Empty;
                    if (!trackedPuuids.Contains(puuid))
                        continue;

                    var userId = trackedPlayers.First(p => p.Puuid == puuid).UserId;
                    participants.Add(new MatchParticipant
                    {
                        UserId = userId,
                        Puuid = puuid,
                        ChampionId = participant.GetProperty("championId").GetInt32(),
                        Team = participant.GetProperty("teamId").GetInt32() == 100 ? LobbyTeam.Blue : LobbyTeam.Red,
                        Kills = participant.GetProperty("kills").GetInt32(),
                        Deaths = participant.GetProperty("deaths").GetInt32(),
                        Assists = participant.GetProperty("assists").GetInt32(),
                        Win = participant.GetProperty("win").GetBoolean(),
                        RawStats = participant.GetRawText(),
                    });
                }

                // Require at least half of the tracked lobby to be found in the match before accepting it.
                if (participants.Count == 0 || participants.Count < trackedPlayers.Count / 2)
                    continue;

                var match = new Match
                {
                    ClubId = clubId,
                    LobbyId = lobbyId,
                    RiotMatchId = matchId,
                    PlayedAt = playedAt,
                    QueueId = queueId,
                    RawPayload = rawJson,
                };

                return await matchRepository.InsertAsync(match, participants);
            }
        }

        return null;
    }

    private async Task<List<(Guid UserId, string Puuid)>> GetTrackedPlayersAsync(Guid lobbyId, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(Guid UserId, string Puuid)>(
            """
            select u.id "UserId", u.riot_puuid "Puuid"
            from lobby_players lp
            join users u on u.id = lp.user_id
            where lp.lobby_id = @lobbyId and u.riot_puuid is not null
            """,
            new { lobbyId });
        return rows.ToList();
    }
}
