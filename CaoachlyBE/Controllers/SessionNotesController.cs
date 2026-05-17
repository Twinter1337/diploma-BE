using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaoachlyBE.Models.Dtos.Notes;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/session-notes")]
[Authorize]
public class SessionNotesController(ISessionNoteService sessionNoteService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SessionNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateSessionNoteDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            var result = await sessionNoteService.CreateAsync(userId, dto);
            return CreatedAtAction(nameof(GetByBookingId), new { bookingId = result.BookingId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SessionNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBookingId([FromQuery] Guid bookingId)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            var result = await sessionNoteService.GetByBookingIdAsync(bookingId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(SessionNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionNoteDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            var result = await sessionNoteService.UpdateAsync(id, userId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            await sessionNoteService.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden." }); }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }
}
