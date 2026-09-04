using LolStatTrak.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

/// <summary>Champion list (from Data Dragon) used by the ban picker and lobby cards.</summary>
[ApiController]
[Route("api/champions")]
[Authorize]
public class ChampionsController(ChampionCatalogService championCatalog) : ControllerBase
{
    /// <summary>
    /// The payload is identical for every user and only changes when Data Dragon ships a new
    /// patch, so the patch version doubles as the ETag: clients revalidate with If-None-Match
    /// and get a 304 for free most of the time.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var catalog = await championCatalog.GetAsync(ct);
        var etag = $"\"{catalog.Version}\"";

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=43200, stale-while-revalidate=86400";

        if (Request.Headers.IfNoneMatch.Any(v => v == etag))
            return StatusCode(StatusCodes.Status304NotModified);

        return Ok(new { catalog.Version, catalog.Champions });
    }
}
