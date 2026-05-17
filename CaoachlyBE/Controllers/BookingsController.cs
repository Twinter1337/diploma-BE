using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaoachlyBE.Models.Dtos.Bookings;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController(IBookingService bookingService, IUserRepository userRepo) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var clientIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (clientIdClaim is null || !Guid.TryParse(clientIdClaim, out var clientId))
            return Unauthorized();

        var clientEmail = User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;

        var user = await userRepo.GetByIdAsync(clientId);
        var clientName = user is not null ? $"{user.FirstName} {user.LastName}" : clientEmail;

        try
        {
            var result = await bookingService.CreateAsync(clientId, clientEmail, clientName, dto);
            return CreatedAtAction(nameof(Create), new { id = result.BookingId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("overlaps"))
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(typeof(CancelBookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var clientIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(clientIdClaim, out var clientId))
            return Unauthorized();

        try
        {
            var result = await bookingService.CancelAsync(id, clientId);
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
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/retry-payment")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(typeof(CreateBookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryPayment(Guid id)
    {
        var clientIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(clientIdClaim, out var clientId))
            return Unauthorized();

        var clientEmail = User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;

        try
        {
            var result = await bookingService.RetryPaymentAsync(id, clientId, clientEmail);
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
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetMine() => StatusCode(501, new { message = "Not implemented yet." });

    [HttpGet("{id:guid}")]
    [Authorize]
    public IActionResult GetById(Guid id) => StatusCode(501, new { message = "Not implemented yet." });
}
