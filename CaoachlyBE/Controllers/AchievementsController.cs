using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaoachlyBE.Models.Dtos.Achievements;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/achievements")]
[Authorize]
public class AchievementsController(IAchievementService achievementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AchievementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnearned()
    {
        var rawId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawId, out var userId))
            return Unauthorized();

        var result = await achievementService.GetUnearnedAchievementsAsync(userId);
        return Ok(result);
    }
}
