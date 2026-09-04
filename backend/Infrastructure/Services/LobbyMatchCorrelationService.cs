using System.Text.Json;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Data;
using LolStatTrak.Infrastructure.Repositories;
using Dapper;

namespace LolStatTrak.Infrastructure.Services;

public enum CorrelationOutcome
{
    /// <summary>A match was found and persisted (or was already attached to this lobby).</summary>
    Found,
    /// <summary>Nobody in the lobby has linked a Riot account, so there is nothing to search with.</summary>
    NoLinkedPlayers,
    /// <summary>Linked players exist but no recent match containing enough of them was found yet.</summary>
    NotFoundYet,
    /// <summary>Riot API call failed (rate limit, key expired, outage).</summary>
    RiotError,
}

public record CorrelationResult(
    CorrelationOutcome Outcome,
    Guid? MatchId,
    int LinkedPlayers,
    int TotalPlayers,
    string? Detail = null)
{
    public static CorrelationResult Found(Guid matchId, int linked, int total) => new(CorrelationOutcome.Found, matchId, linked, total);
}

/// <summary>
/// Correlates a played lobby with a finished Riot match: pulls each linked player's recent match
/// ids, fetches the raw payload, and accepts the first match played after the lobby was created
/// that contains enough of the lobby's linked players. Persists the match + tracked participants.
/// </summary>
public class LobbyMatchCorrelationService(
    RiotApiClient riotClient,
    MatchRepository matchRepository,
    NpgsqlConnectionFactory connectionFactory)
{
    private const int CustomGameQueueId = 0;

    public async Task<CorrelationResult> TryCorrelateAndPersistAsync(Guid lobbyId, Guid clubId, DateTimeOffset lobbyCreatedAt, CancellationToken ct = default)
    {
        var existing = await matchRepository.GetIdForLobbyAsync(lobbyId);
        var allPlayers = await GetPlayersAsync(lobbyId, ct);
        var tracked = allPlayers.Where(p => p.Puuid is not null).Select(p => (p.UserId, Puuid: p.Puuid!)).ToList();

        if (existing is not null)
            return CorrelationResult.Found(existing.Value, tracked.Count, allPlayers.Count);
        if (tracked.Count == 0)
            return new CorrelationResult(CorrelationOutcome.NoLinkedPlayers, null, 0, allPlayers.Count);

        var trackedPuuids = tracked.Select(p => p.Puuid).ToHashSet();
        var minParticipants = MinimumParticipants(tracked.Count);
        var checkedMatchIds = new HashSet<string>();

        try
        {
            foreach (var player in tracked)
            {
                var matchIds = await riotClient.GetRecentMatchIdsAsync(player.Puuid, count: 5, ct);
                foreach (var matchId in matchIds)
                {
                    if (!checkedMatchIds.Add(matchId) || await matchRepository.ExistsAsync(matchId))
                        continue;

                    var rawJson = await riotClient.GetMatchRawJsonAsync(matchId, ct);
                    if (rawJson is null)
                        continue;

                    var candidate = TryBuildMatch(rawJson, matchId, lobbyId, clubId, lobbyCreatedAt, tracked, trackedPuuids, minParticipants);
                    if (candidate is null)
                        continue;

                    var id = await matchRepository.InsertAsync(candidate.Value.Match, candidate.Value.Participants);
                    return CorrelationResult.Found(id, tracked.Count, allPlayers.Count);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            return new CorrelationResult(CorrelationOutcome.RiotError, null, tracked.Count, allPlayers.Count, ex.Message);
        }

        return new CorrelationResult(CorrelationOutcome.NotFoundYet, null, tracked.Count, allPlayers.Count);
    }

    /// <summary>
    /// With one linked player any of their games would "match", so we then insist on it being a
    /// custom game. With two or more we require at least two linked players and at least half of them.
    /// </summary>
    private static int MinimumParticipants(int trackedCount) =>
        trackedCount <= 1 ? 1 : Math.Max(2, (int)Math.Ceiling(trackedCount / 2.0));

    private static (Match Match, List<MatchParticipant> Participants)? TryBuildMatch(
        string rawJson,
        string matchId,
        Guid lobbyId,
        Guid clubId,
        DateTimeOffset lobbyCreatedAt,
        List<(Guid UserId, string Puuid)> tracked,
        HashSet<string> trackedPuuids,
        int minParticipants)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var info = doc.RootElement.GetProperty("info");

        var gameEndMs = info.TryGetProperty("gameEndTimestamp", out var endProp)
            ? endProp.GetInt64()
            : info.GetProperty("gameCreation").GetInt64();
        var playedAt = DateTimeOffset.FromUnixTimeMilliseconds(gameEndMs);
        if (playedAt < lobbyCreatedAt)
            return null;

        var queueId = info.GetProperty("queueId").GetInt32();
        if (tracked.Count <= 1 && queueId != CustomGameQueueId)
            return null;

        var participants = new List<MatchParticipant>();
        foreach (var participant in info.GetProperty("participants").EnumerateArray())
        {
            var puuid = participant.GetProperty("puuid").GetString() ?? string.Empty;
            if (!trackedPuuids.Contains(puuid))
                continue;

            participants.Add(new MatchParticipant
            {
                UserId = tracked.First(p => p.Puuid == puuid).UserId,
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

        if (participants.Count < minParticipants)
            return null;

        var match = new Match
        {
            ClubId = clubId,
            LobbyId = lobbyId,
            RiotMatchId = matchId,
            PlayedAt = playedAt,
            QueueId = queueId,
            RiotGameMode = info.TryGetProperty("gameMode", out var modeProp) ? modeProp.GetString() : null,
            GameDurationSeconds = info.TryGetProperty("gameDuration", out var durProp) ? durProp.GetInt32() : null,
            RawPayload = rawJson,
        };

        return (match, participants);
    }

    private async Task<List<(Guid UserId, string? Puuid)>> GetPlayersAsync(Guid lobbyId, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(Guid UserId, string? Puuid)>(
            """
            select u.id "UserId", u.riot_puuid "Puuid"
            from lobby_players lp
            join users u on u.id = lp.user_id
            where lp.lobby_id = @lobbyId
            """,
            new { lobbyId });
        return rows.ToList();
    }
}
