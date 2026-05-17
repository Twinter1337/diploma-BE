using System.Security.Claims;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/slots")]
[Authorize]
public class SlotsController(ITrainerService trainerService) : ControllerBase
{
    /// <summary>Partially update a schedule slot.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ScheduleSlotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSlot(Guid id, [FromBody] UpdateScheduleSlotDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            var result = await trainerService.UpdateSlotAsync(id, requestingUserId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden." });
        }
    }
}
