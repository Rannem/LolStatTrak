using System.Net.Http.Json;
using LolStatTrak.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LolStatTrak.Infrastructure.Services;

public record RiotAccount(string Puuid, string GameName, string TagLine);

public record RiotMatchDto(string MatchId, string RawJson, DateTimeOffset PlayedAt, int QueueId);

/// <summary>
/// Thin wrapper over Riot's account-v1 and match-v5 REST APIs. All Riot HTTP access is
/// centralized here so rate-limit handling (429 + Retry-After) lives in one place.
/// </summary>
public class RiotApiClient(HttpClient httpClient, IOptions<RiotApiOptions> options)
{
    private readonly RiotApiOptions _options = options.Value;

    /// <summary>Resolves a Riot ID (gameName#tagLine) to a PUUID, once per user, then cached by the caller.</summary>
    public async Task<RiotAccount?> ResolveAccountAsync(string gameName, string tagLine, CancellationToken ct = default)
    {
        var url = $"https://{_options.RegionalRouting}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}";
        using var response = await SendAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken: ct);
        return payload is null ? null : new RiotAccount(payload.Puuid, payload.GameName, payload.TagLine);
    }

    /// <summary>Lists recent match ids for a PUUID (used to find newly finished custom games).</summary>
    public async Task<IReadOnlyList<string>> GetRecentMatchIdsAsync(string puuid, int count = 10, CancellationToken ct = default)
    {
        var url = $"https://{_options.RegionalRouting}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?start=0&count={count}";
        using var response = await SendAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken: ct) ?? [];
    }

    /// <summary>Fetches the full match-v5 payload for correlation + stat persistence.</summary>
    public async Task<string?> GetMatchRawJsonAsync(string matchId, CancellationToken ct = default)
    {
        var url = $"https://{_options.RegionalRouting}.api.riotgames.com/lol/match/v5/matches/{matchId}";
        using var response = await SendAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _options.ApiKey);
        var response = await httpClient.SendAsync(request, ct);

        // Personal key rate limit is tight (20/1s, 100/2min) — honor Retry-After on 429 with one retry.
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
            await Task.Delay(retryAfter, ct);
            using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
            retryRequest.Headers.Add("X-Riot-Token", _options.ApiKey);
            response = await httpClient.SendAsync(retryRequest, ct);
        }

        return response;
    }

    private record AccountResponse(string Puuid, string GameName, string TagLine);
}
