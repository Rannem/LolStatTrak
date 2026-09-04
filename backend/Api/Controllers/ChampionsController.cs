using LolStatTrak.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LolStatTrak.Api.Controllers;

/// <summary>Public champion list (from Data Dragon) used by the ban picker and lobby cards.</summary>
[ApiController]
[Route("api/champions")]
[Authorize]
public class ChampionsController(ChampionCatalogService championCatalog) : ControllerBase
{
    [HttpGet]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var catalog = await championCatalog.GetAsync(ct);
        return Ok(new { catalog.Version, catalog.Champions });
    }
}
