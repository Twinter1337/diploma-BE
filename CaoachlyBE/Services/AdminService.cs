using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos;
using CaoachlyBE.Models.Dtos.Admin;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class AdminService(
    ISupportTicketRepository ticketRepo,
    ITrainerDocumentRepository documentRepo,
    IUserRepository userRepo,
    ITrainerInfoRepository trainerInfoRepo,
    IBlobStorageService blobStorage,
    IEmailService emailService,
    IUnitOfWork uow) : IAdminService
{
    private const string DocumentsContainer = "trainer-documents";
    private static readonly TimeSpan DocumentSasTtl = TimeSpan.FromMinutes(30);


    public async Task<AdminStatsDto> GetStatsAsync()
    {
        var openTickets = await ticketRepo.CountByStatusAsync(TicketStatus.Open);
        var pendingDocuments = await documentRepo.CountPendingAsync();
        var unassigned = await ticketRepo.CountUnassignedActiveAsync();

        return new AdminStatsDto
        {
            OpenTickets = openTickets,
            PendingDocuments = pendingDocuments,
            UnassignedTickets = unassigned,
        };
    }

    public async Task<PagedResultDto<AdminTicketListItemDto>> ListTicketsAsync(
        string type, string? assignedTo, string? search, int page, int pageSize)
    {
        var normalizedType = (type ?? "all").Trim().ToLowerInvariant();

        Guid? assigneeId = null;
        var unassignedOnly = false;
        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            if (assignedTo.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
                unassignedOnly = true;
            else if (Guid.TryParse(assignedTo, out var parsed))
                assigneeId = parsed;
            else
                throw new InvalidOperationException("assignedTo must be a uuid or 'unassigned'.");
        }

        bool? documentLinked = normalizedType switch
        {
            "document" => true,
            "request" => false,
            _ => (bool?)null,
        };

        var (rows, total) = await ticketRepo.SearchAsync(assigneeId, unassignedOnly, search, page, pageSize, documentLinked);

        return new PagedResultDto<AdminTicketListItemDto>
        {
            Items = rows.Select(MapTicketRowToListItem).ToList(),
            TotalCount = total,
            TotalPages = (total + pageSize - 1) / pageSize,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AdminTicketDetailDto?> GetTicketAsync(Guid id)
    {
        var row = await ticketRepo.GetRowByIdAsync(id);
        return row is null ? null : MapTicketRowToDetail(row);
    }

    public async Task<AdminTicketDetailDto> PatchTicketAsync(Guid id, PatchTicketDto dto)
    {
        var existing = await ticketRepo.GetRowByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket not found.");

        if (!dto.Unassign && dto.AssignedTo.HasValue)
        {
            var assignee = await userRepo.GetByIdAsync(dto.AssignedTo.Value);
            if (assignee is null || assignee.Role != UserRole.Admin)
                throw new InvalidOperationException("assignedTo must reference an active admin user.");
        }

        var ok = await ticketRepo.PatchAsync(id, dto.Status, dto.AssignedTo, dto.Unassign, DateTime.UtcNow);
        if (!ok) throw new KeyNotFoundException("Ticket not found.");

        await uow.SaveChangesAsync();

        var updated = await ticketRepo.GetRowByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket not found after update.");
        return MapTicketRowToDetail(updated);
    }

    public async Task<IEnumerable<AdminUserSummaryDto>> GetAdminsAsync()
    {
        var admins = await userRepo.GetByRoleAsync(UserRole.Admin);
        return admins.Select(a => new AdminUserSummaryDto
        {
            Id = a.Id,
            FullName = $"{a.FirstName} {a.LastName}".Trim(),
            AvatarUrl = a.AvatarUrl,
        });
    }

    public async Task<DocumentReviewResultDto> ApproveDocumentAsync(Guid documentId, Guid adminId)
    {
        var doc = await documentRepo.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");
        if (doc.Status != DocumentStatus.Pending)
            throw new InvalidOperationException("Document has already been reviewed.");

        var reviewedAt = DateTime.UtcNow;
        var ok = await documentRepo.ReviewAsync(documentId, DocumentStatus.Approved, adminId, null, reviewedAt);
        if (!ok) throw new KeyNotFoundException("Document not found.");

        await ticketRepo.PatchStatusByDocumentAsync(documentId, TicketStatus.Resolved, reviewedAt);

        await trainerInfoRepo.SetVerificationStatusAsync(doc.TrainerId, VerificationStatus.Verified);

        await uow.SaveChangesAsync();

        return new DocumentReviewResultDto
        {
            DocumentId = documentId,
            Status = (short)DocumentStatus.Approved,
            TicketStatus = (short)TicketStatus.Resolved,
            ReviewedAt = reviewedAt,
        };
    }

    public async Task<DocumentReviewResultDto> RejectDocumentAsync(Guid documentId, Guid adminId, string? rejectionReason)
    {
        var doc = await documentRepo.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");
        if (doc.Status != DocumentStatus.Pending)
            throw new InvalidOperationException("Document has already been reviewed.");

        var reviewedAt = DateTime.UtcNow;
        var ok = await documentRepo.ReviewAsync(documentId, DocumentStatus.Rejected, adminId, rejectionReason, reviewedAt);
        if (!ok) throw new KeyNotFoundException("Document not found.");

        await ticketRepo.PatchStatusByDocumentAsync(documentId, TicketStatus.Closed, reviewedAt);

        await uow.SaveChangesAsync();

        return new DocumentReviewResultDto
        {
            DocumentId = documentId,
            Status = (short)DocumentStatus.Rejected,
            TicketStatus = (short)TicketStatus.Closed,
            ReviewedAt = reviewedAt,
        };
    }

    public async Task ReplyToTicketAsync(Guid ticketId, ReplyToTicketDto dto)
    {
        var ticket = await ticketRepo.GetRowByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        await emailService.SendAdminReplyAsync(dto.SendTo, dto.Subject, dto.Body);
    }

    // ---------- mapping helpers ----------

    private static AdminTicketListItemDto MapTicketRowToListItem(AdminTicketRowModel r) => new()
    {
        Id = r.Id,
        Type = r.RelatedDocumentId.HasValue ? "document" : "request",
        Status = (short)r.Status,
        Subject = r.Subject,
        DocType = r.DocumentType?.ToString().ToLowerInvariant(),
        CreatedBy = new AdminTicketCreatorDto
        {
            Id = r.CreatedById,
            FullName = $"{r.CreatedByFirstName} {r.CreatedByLastName}".Trim(),
            Role = r.CreatedByRole.ToString(),
            Email = r.CreatedByEmail,
            AvatarUrl = r.CreatedByAvatarUrl,
        },
        AssignedTo = r.AssignedToId.HasValue ? new AdminUserSummaryDto
        {
            Id = r.AssignedToId.Value,
            FullName = $"{r.AssignedToFirstName} {r.AssignedToLastName}".Trim(),
            AvatarUrl = r.AssignedToAvatarUrl,
        } : null,
        CreatedAt = r.CreatedAt,
    };

    private AdminTicketDetailDto MapTicketRowToDetail(AdminTicketRowModel r) => new()
    {
        Id = r.Id,
        Type = r.RelatedDocumentId.HasValue ? "document" : "request",
        Status = (short)r.Status,
        Subject = r.Subject,
        Description = r.Description,
        CreatedBy = new AdminTicketCreatorDto
        {
            Id = r.CreatedById,
            FullName = $"{r.CreatedByFirstName} {r.CreatedByLastName}".Trim(),
            Role = r.CreatedByRole.ToString(),
            Email = r.CreatedByEmail,
            AvatarUrl = r.CreatedByAvatarUrl,
        },
        AssignedTo = r.AssignedToId.HasValue ? new AdminUserSummaryDto
        {
            Id = r.AssignedToId.Value,
            FullName = $"{r.AssignedToFirstName} {r.AssignedToLastName}".Trim(),
            AvatarUrl = r.AssignedToAvatarUrl,
        } : null,
        RelatedBookingId = r.RelatedBookingId,
        RelatedTrainerId = r.RelatedDocumentId.HasValue ? r.CreatedById : null,
        Document = r.RelatedDocumentId.HasValue ? new AdminTicketDocumentDto
        {
            Id = r.RelatedDocumentId.Value,
            Type = r.DocumentType?.ToString().ToLowerInvariant() ?? "other",
            FileName = r.DocumentFileName ?? string.Empty,
            FileSizeBytes = r.DocumentFileSizeBytes ?? 0,
            FileUrl = string.IsNullOrEmpty(r.DocumentFileUrl)
                ? string.Empty
                : blobStorage.GetReadSasUrl(r.DocumentFileUrl, DocumentsContainer, DocumentSasTtl),
            Status = (short)(r.DocumentStatus ?? Enums.DocumentStatus.Pending),
        } : null,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
