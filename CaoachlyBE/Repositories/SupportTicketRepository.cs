using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class SupportTicketRepository(AppDbContext context) : ISupportTicketRepository
{
    private IQueryable<AdminTicketRowModel> BaseQuery() =>
        from t in context.SupportTickets.AsNoTracking()
        join c in context.Users.AsNoTracking() on t.CreatedBy equals c.Id
        join a in context.Users.AsNoTracking() on t.AssignedTo equals a.Id into ag
        from a in ag.DefaultIfEmpty()
        join d in context.TrainerDocuments.AsNoTracking() on t.RelatedDocumentId equals d.Id into dg
        from d in dg.DefaultIfEmpty()
        select new AdminTicketRowModel
        {
            Id = t.Id,
            Subject = t.Subject,
            Description = t.Description,
            Status = (TicketStatus)t.Status,
            RelatedBookingId = t.RelatedBookingId,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,

            CreatedById = c.Id,
            CreatedByFirstName = c.FirstName,
            CreatedByLastName = c.LastName,
            CreatedByEmail = c.Email,
            CreatedByRole = (UserRole)c.Role,
            CreatedByAvatarUrl = c.AvatarUrl,

            AssignedToId = a == null ? (Guid?)null : a.Id,
            AssignedToFirstName = a == null ? null : a.FirstName,
            AssignedToLastName = a == null ? null : a.LastName,
            AssignedToAvatarUrl = a == null ? null : a.AvatarUrl,

            RelatedDocumentId = t.RelatedDocumentId,
            DocumentFileName = d == null ? null : d.FileName,
            DocumentFileSizeBytes = d == null ? (int?)null : d.FileSizeBytes,
            DocumentFileUrl = d == null ? null : d.FileUrl,
            DocumentType = d == null ? (DocumentType?)null : (DocumentType)d.DocumentType,
            DocumentStatus = d == null ? (DocumentStatus?)null : (DocumentStatus)d.Status,
        };

    public Task<AdminTicketRowModel?> GetRowByIdAsync(Guid id) =>
        BaseQuery().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<(IReadOnlyList<AdminTicketRowModel> Items, int Total)> SearchAsync(
        Guid? assignedTo, bool unassignedOnly, string? search, int page, int pageSize,
        bool? documentLinked = null)
    {
        var q = BaseQuery();

        if (documentLinked == true)
            q = q.Where(r => r.RelatedDocumentId != null);
        else if (documentLinked == false)
            q = q.Where(r => r.RelatedDocumentId == null);

        if (unassignedOnly)
            q = q.Where(r => r.AssignedToId == null);
        else if (assignedTo.HasValue)
            q = q.Where(r => r.AssignedToId == assignedTo.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(r =>
                EF.Functions.ILike(r.Subject, pattern) ||
                EF.Functions.ILike(r.CreatedByFirstName + " " + r.CreatedByLastName, pattern) ||
                EF.Functions.ILike(r.CreatedByEmail, pattern) ||
                (r.AssignedToFirstName != null && EF.Functions.ILike(r.AssignedToFirstName + " " + r.AssignedToLastName, pattern))
            );
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<int> CountByStatusAsync(TicketStatus status) =>
        context.SupportTickets.AsNoTracking().CountAsync(t => t.Status == (short)status);

    public Task<int> CountUnassignedActiveAsync() =>
        context.SupportTickets.AsNoTracking()
            .CountAsync(t => t.AssignedTo == null && t.Status != (short)TicketStatus.Closed);

    public async Task AddAsync(SupportTicketModel model)
    {
        var entity = new Entities.SupportTicket
        {
            Id = model.Id,
            CreatedBy = model.CreatedBy,
            Subject = model.Subject,
            Description = model.Description,
            Status = (short)model.Status,
            RelatedBookingId = model.RelatedBookingId,
            RelatedDocumentId = model.RelatedDocumentId,
            AssignedTo = model.AssignedTo,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
        await context.SupportTickets.AddAsync(entity);
    }

    public async Task<bool> PatchStatusByDocumentAsync(Guid documentId, TicketStatus status, DateTime updatedAt)
    {
        var entity = await context.SupportTickets.FirstOrDefaultAsync(t => t.RelatedDocumentId == documentId);
        if (entity is null) return false;
        entity.Status = (short)status;
        entity.UpdatedAt = updatedAt;
        return true;
    }

    public async Task<bool> DetachAndCloseByDocumentAsync(Guid documentId, DateTime updatedAt)
    {
        var entity = await context.SupportTickets.FirstOrDefaultAsync(t => t.RelatedDocumentId == documentId);
        if (entity is null) return false;
        entity.RelatedDocumentId = null;
        entity.Status = (short)TicketStatus.Closed;
        entity.UpdatedAt = updatedAt;
        return true;
    }

    public async Task<bool> PatchAsync(Guid id, TicketStatus? status, Guid? assignedTo, bool unassign, DateTime updatedAt)
    {
        var entity = await context.SupportTickets.FindAsync(id);
        if (entity is null) return false;

        if (status.HasValue)
            entity.Status = (short)status.Value;

        if (unassign)
            entity.AssignedTo = null;
        else if (assignedTo.HasValue)
            entity.AssignedTo = assignedTo.Value;

        entity.UpdatedAt = updatedAt;
        return true;
    }
}
