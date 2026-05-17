using CaoachlyBE.Models.Dtos.Stats;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(IStatsService statsService) : ControllerBase
{
    /// <summary>Returns aggregated platform statistics.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlatformStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var stats = await statsService.GetPlatformStatsAsync();
        return Ok(stats);
    }
}
