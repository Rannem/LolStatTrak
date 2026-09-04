using LolStatTrak.Domain.Entities;

namespace LolStatTrak.Domain.Services;

/// <summary>Pure domain logic for rolling random teams + champions for a lobby, honoring a club's bans.</summary>
public class RandomizerService
{
    /// <summary>
    /// Splits <paramref name="userIds"/> into two even (as possible) random teams and, when
    /// <paramref name="assignChampions"/> is set, gives each player a random champion drawn from
    /// <paramref name="allChampionIds"/> minus <paramref name="bannedChampionIds"/>.
    /// Champions are drawn without replacement across the whole lobby when the pool is large enough.
    /// </summary>
    public IReadOnlyList<LobbyPlayer> Roll(
        Guid lobbyId,
        IReadOnlyList<Guid> userIds,
        IReadOnlyCollection<int> allChampionIds,
        IReadOnlyCollection<int> bannedChampionIds,
        bool assignChampions = true)
    {
        if (userIds.Count == 0)
            return [];

        var random = Random.Shared;

        var shuffledUsers = userIds.OrderBy(_ => random.Next()).ToList();
        var teamSplit = shuffledUsers
            .Select((userId, index) => (userId, team: index % 2 == 0 ? LobbyTeam.Blue : LobbyTeam.Red))
            .ToList();

        if (!assignChampions)
        {
            return teamSplit
                .Select(t => new LobbyPlayer { LobbyId = lobbyId, UserId = t.userId, AssignedTeam = t.team, AssignedChampionId = null })
                .ToList();
        }

        var championPool = allChampionIds.Except(bannedChampionIds).ToList();
        if (championPool.Count == 0)
            throw new InvalidOperationException("No champions available after applying the club's ban list.");

        // Draw without replacement while the pool covers everyone, otherwise allow repeats.
        var shuffledChampions = championPool.OrderBy(_ => random.Next()).ToList();
        var useWithoutReplacement = shuffledChampions.Count >= teamSplit.Count;

        var result = new List<LobbyPlayer>(teamSplit.Count);
        for (var i = 0; i < teamSplit.Count; i++)
        {
            var championId = useWithoutReplacement
                ? shuffledChampions[i]
                : championPool[random.Next(championPool.Count)];

            result.Add(new LobbyPlayer
            {
                LobbyId = lobbyId,
                UserId = teamSplit[i].userId,
                AssignedTeam = teamSplit[i].team,
                AssignedChampionId = championId,
            });
        }

        return result;
    }
}
