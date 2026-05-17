using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tickets;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/tickets")]
public class SupportTicketsController(ISupportTicketService supportTicketService) : ControllerBase
{
    [HttpPost("booking")]
    [Authorize(Roles = "Client,Trainer")]
    [ProducesResponseType(typeof(SupportTicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateForBooking([FromBody] CreateBookingTicketDto dto)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var roleClaim = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role");
        if (!Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            var result = await supportTicketService.CreateForBookingAsync(userId, role, dto);
            return CreatedAtAction(nameof(CreateForBooking), new { id = result.Id }, result);
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
