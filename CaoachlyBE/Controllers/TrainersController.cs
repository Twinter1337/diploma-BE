using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos;
using CaoachlyBE.Models.Dtos.Bookings;
using CaoachlyBE.Models.Dtos.Documents;
using CaoachlyBE.Models.Dtos.Reviews;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Models.Dtos.Trainers;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/trainers")]
[Authorize]
public class TrainersController(ITrainerService trainerService, IReviewService reviewService) : ControllerBase
{
    /// <summary>Get public profile of a trainer by their user ID.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TrainerPublicProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(Guid id)
    {
        var result = await trainerService.GetPublicProfileAsync(id);
        if (result is null)
            return NotFound(new { message = "Trainer not found." });
        return Ok(result);
    }

    /// <summary>Search trainers with optional filters, pagination and sorting.</summary>
    [HttpPost("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResultDto<TrainerListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromBody] TrainerSearchFilterDto filter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 9,
        [FromQuery] string sortBy = "rating",
        [FromQuery] string sortOrder = "desc")
    {
        if (page < 1 || pageSize < 1 || pageSize > 50)
            return BadRequest(new { message = "page must be ≥ 1 and pageSize must be between 1 and 50." });

        var allowedSortBy = new[] { "rating", "price", "experience" };
        var allowedSortOrder = new[] { "asc", "desc" };
        if (!allowedSortBy.Contains(sortBy) || !allowedSortOrder.Contains(sortOrder))
            return BadRequest(new { message = "sortBy must be one of: rating, price, experience. sortOrder must be asc or desc." });

        var result = await trainerService.SearchAsync(filter, page, pageSize, sortBy, sortOrder);
        return Ok(result);
    }

    /// <summary>Partial update of a trainer's profile (onboarding steps 1, 2 &amp; 4).</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] OnboardTrainerRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            await trainerService.UpdateProfileAsync(id, requestingUserId, dto);
            return NoContent();
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

    /// <summary>Get all available slots for a trainer.</summary>
    [HttpGet("{id:guid}/slots")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<TrainerSlotListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableSlots(Guid id, [FromQuery] bool isTrainer, [FromQuery] SlotFilterDto filter)
    {
        try
        {
            var result = await trainerService.GetAvailableSlotsAsync(id, isTrainer, filter);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Get stats for a trainer.</summary>
    [HttpGet("{id:guid}/stats")]
    [ProducesResponseType(typeof(TrainerStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStats(Guid id)
    {
        try
        {
            var result = await trainerService.GetStatsAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Get all-time clients for a trainer.</summary>
    [HttpGet("{id:guid}/clients")]
    [ProducesResponseType(typeof(IEnumerable<TrainerClientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClients(Guid id)
    {
        try
        {
            var result = await trainerService.GetClientsAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Get future bookings for a trainer.</summary>
    [HttpGet("{id:guid}/bookings")]
    [ProducesResponseType(typeof(IEnumerable<TrainerBookingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFutureBookings(Guid id)
    {
        try
        {
            var result = await trainerService.GetFutureBookingsAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Get slot counts for a trainer.</summary>
    [HttpGet("{id:guid}/slot-count")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TrainerSlotCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlotCount(Guid id)
    {
        try
        {
            var result = await trainerService.GetSlotCountAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Create a single schedule slot for a trainer.</summary>
    [HttpPost("{id:guid}/slots")]
    [ProducesResponseType(typeof(ScheduleSlotDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSlot(Guid id, [FromBody] CreateScheduleSlotDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            var result = await trainerService.CreateSlotAsync(id, requestingUserId, dto);
            return StatusCode(StatusCodes.Status201Created, result);
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

    /// <summary>Cancel a schedule slot. Fully refunds all paid bookings and notifies clients.</summary>
    [HttpDelete("{id:guid}/slots/{slotId:guid}")]
    [Authorize(Roles = "Trainer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteSlot(Guid id, Guid slotId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            await trainerService.DeleteSlotAsync(slotId, requestingUserId);
            return NoContent();
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

    /// <summary>Get reviews for a trainer.</summary>
    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<TrainerReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReviews(Guid id)
    {
        try
        {
            var result = await reviewService.GetByTrainerIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Get all documents for a trainer. Only the trainer themselves can access this.</summary>
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(IEnumerable<TrainerDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocuments(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            var result = await trainerService.GetDocumentsAsync(id, requestingUserId);
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

    /// <summary>Upload a document for a trainer (multipart/form-data).</summary>
    [HttpPost("{id:guid}/documents")]
    [ProducesResponseType(typeof(UploadDocumentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadDocument(Guid id, [FromForm] UploadDocumentRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            var result = await trainerService.UploadDocumentAsync(id, requestingUserId, dto);
            return StatusCode(StatusCodes.Status201Created, result);
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
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Delete a document belonging to the authenticated trainer.</summary>
    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [Authorize(Roles = "Trainer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            await trainerService.DeleteDocumentAsync(id, documentId, requestingUserId);
            return NoContent();
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

    /// <summary>Get booking history for a specific client of this trainer.</summary>
    [HttpGet("{id:guid}/clients/{clientId:guid}/bookings")]
    [Authorize(Roles = "Trainer")]
    [ProducesResponseType(typeof(PagedResultDto<TrainerBookingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientBookings(
        Guid id,
        Guid clientId,
        [FromQuery] BookingStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var requestingUserId))
            return Unauthorized();

        try
        {
            var result = await trainerService.GetClientBookingsAsync(id, requestingUserId, clientId, status, page, pageSize);
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
