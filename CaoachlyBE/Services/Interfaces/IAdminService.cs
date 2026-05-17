using CaoachlyBE.Models.Dtos;
using CaoachlyBE.Models.Dtos.Admin;

namespace CaoachlyBE.Services.Interfaces;

public interface IAdminService
{
    Task<AdminStatsDto> GetStatsAsync();

    Task<PagedResultDto<AdminTicketListItemDto>> ListTicketsAsync(
        string type, string? assignedTo, string? search, int page, int pageSize);

    Task<AdminTicketDetailDto?> GetTicketAsync(Guid id);

    Task<AdminTicketDetailDto> PatchTicketAsync(Guid id, PatchTicketDto dto);

    Task<IEnumerable<AdminUserSummaryDto>> GetAdminsAsync();

    Task<DocumentReviewResultDto> ApproveDocumentAsync(Guid documentId, Guid adminId);

    Task<DocumentReviewResultDto> RejectDocumentAsync(Guid documentId, Guid adminId, string? rejectionReason);

    Task ReplyToTicketAsync(Guid ticketId, ReplyToTicketDto dto);
}
