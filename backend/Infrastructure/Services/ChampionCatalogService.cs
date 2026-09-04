using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LolStatTrak.Infrastructure.Services;

/// <summary>A champion as exposed to the frontend / randomizer, sourced from Riot's Data Dragon.</summary>
public record ChampionInfo(
    int Id,
    string Alias,
    string Name,
    string Title,
    string IconUrl,
    string LoadingArtUrl,
    string SplashUrl);

public record ChampionCatalog(string Version, IReadOnlyList<ChampionInfo> Champions)
{
    public IReadOnlyCollection<int> AllIds { get; } = Champions.Select(c => c.Id).ToList();
}

/// <summary>
/// Pulls the champion list from Data Dragon (Riot's public static-data CDN — no API key needed)
/// and caches it in memory, refreshing periodically so newly released champions show up
/// without a redeploy. Registered as a singleton.
/// </summary>
public class ChampionCatalogService(HttpClient httpClient, ILogger<ChampionCatalogService> logger)
{
    private const string DdragonBase = "https://ddragon.leagueoflegends.com";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private ChampionCatalog? _cached;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public async Task<ChampionCatalog> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < RefreshInterval)
            return _cached;

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < RefreshInterval)
                return _cached;

            try
            {
                _cached = await FetchAsync(ct);
                _cachedAt = DateTimeOffset.UtcNow;
                logger.LogInformation("Loaded {Count} champions from Data Dragon {Version}", _cached.Champions.Count, _cached.Version);
            }
            catch (Exception ex) when (_cached is not null)
            {
                // Keep serving the stale copy rather than breaking rolls if the CDN hiccups.
                logger.LogWarning(ex, "Data Dragon refresh failed; serving cached champion list {Version}", _cached.Version);
                _cachedAt = DateTimeOffset.UtcNow;
            }

            return _cached;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<ChampionCatalog> FetchAsync(CancellationToken ct)
    {
        var versions = await httpClient.GetFromJsonAsync<string[]>($"{DdragonBase}/api/versions.json", ct)
            ?? throw new InvalidOperationException("Data Dragon returned no versions.");
        var version = versions[0];

        var payload = await httpClient.GetFromJsonAsync<ChampionJson>(
            $"{DdragonBase}/cdn/{version}/data/en_US/champion.json", ct)
            ?? throw new InvalidOperationException("Data Dragon returned no champion data.");

        var champions = payload.Data.Values
            .Select(c => new ChampionInfo(
                Id: int.Parse(c.Key),
                Alias: c.Id,
                Name: c.Name,
                Title: c.Title,
                IconUrl: $"{DdragonBase}/cdn/{version}/img/champion/{c.Image.Full}",
                LoadingArtUrl: $"{DdragonBase}/cdn/img/champion/loading/{c.Id}_0.jpg",
                SplashUrl: $"{DdragonBase}/cdn/img/champion/splash/{c.Id}_0.jpg"))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ChampionCatalog(version, champions);
    }

    private sealed class ChampionJson
    {
        [JsonPropertyName("data")]
        public Dictionary<string, ChampionEntry> Data { get; set; } = new();
    }

    private sealed class ChampionEntry
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;      // alias, e.g. "MonkeyKing"
        [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;    // numeric id as string, e.g. "62"
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;  // display name, e.g. "Wukong"
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("image")] public ChampionImage Image { get; set; } = new();
    }

    private sealed class ChampionImage
    {
        [JsonPropertyName("full")] public string Full { get; set; } = string.Empty;
    }
}
