using System.Security.Claims;
using CaoachlyBE.Models.Dtos;
using CaoachlyBE.Models.Dtos.Admin;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    /// <summary>Header counters: open tickets, pending documents, unassigned active tickets.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AdminStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var result = await adminService.GetStatsAsync();
        return Ok(result);
    }

    /// <summary>Kanban list. Unifies support tickets + pending trainer documents.</summary>
    [HttpGet("support-tickets")]
    [ProducesResponseType(typeof(PagedResultDto<AdminTicketListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListTickets(
        [FromQuery] string type = "all",
        [FromQuery] string? assignedTo = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (page < 1 || pageSize < 1 || pageSize > 200)
            return BadRequest(new { message = "page must be ≥ 1 and pageSize must be between 1 and 200." });

        var allowedTypes = new[] { "all", "request", "document" };
        if (!allowedTypes.Contains(type.ToLowerInvariant()))
            return BadRequest(new { message = "type must be one of: all, request, document." });

        try
        {
            var result = await adminService.ListTicketsAsync(type, assignedTo, search, page, pageSize);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Ticket detail. Works for both support tickets and pending/reviewed documents.</summary>
    [HttpGet("support-tickets/{id:guid}")]
    [ProducesResponseType(typeof(AdminTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var result = await adminService.GetTicketAsync(id);
        if (result is null)
            return NotFound(new { message = "Ticket not found." });
        return Ok(result);
    }

    /// <summary>Partial update: status and/or assignee. Request tickets only — documents use approve/reject.</summary>
    [HttpPatch("support-tickets/{id:guid}")]
    [ProducesResponseType(typeof(AdminTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchTicket(Guid id, [FromBody] PatchTicketDto dto)
    {
        try
        {
            var result = await adminService.PatchTicketAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>List active admin users (for assignee picker/filter).</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<AdminUserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdmins()
    {
        var result = await adminService.GetAdminsAsync();
        return Ok(result);
    }

    /// <summary>Approve a pending trainer document.</summary>
    [HttpPost("trainer-documents/{id:guid}/approve")]
    [ProducesResponseType(typeof(DocumentReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveDocument(Guid id)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var result = await adminService.ApproveDocumentAsync(id, adminId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Reject a pending trainer document with an optional reason.</summary>
    [HttpPost("trainer-documents/{id:guid}/reject")]
    [ProducesResponseType(typeof(DocumentReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectDocument(Guid id, [FromBody] RejectDocumentRequestDto dto)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var result = await adminService.RejectDocumentAsync(id, adminId, dto.RejectionReason);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Send a custom email reply to a client or trainer for a given support ticket.</summary>
    [HttpPost("support-tickets/{id:guid}/reply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplyToTicket(Guid id, [FromBody] ReplyToTicketDto dto)
    {
        try
        {
            await adminService.ReplyToTicketAsync(id, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
