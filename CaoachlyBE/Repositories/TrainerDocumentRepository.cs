using CaoachlyBE.Entities;
using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class TrainerDocumentRepository(AppDbContext context) : ITrainerDocumentRepository
{
    public async Task AddAsync(TrainerDocumentModel model)
    {
        var entity = new TrainerDocument
        {
            Id = model.Id,
            TrainerId = model.TrainerId,
            FileUrl = model.FileUrl,
            FileName = model.FileName,
            FileSizeBytes = model.FileSizeBytes,
            DocumentType = (short)model.DocumentType,
            Status = (short)model.Status,
            UploadedAt = model.UploadedAt,
        };
        await context.TrainerDocuments.AddAsync(entity);
    }

    public async Task<TrainerDocumentModel?> GetByIdAsync(Guid documentId)
    {
        var d = await context.TrainerDocuments.FindAsync(documentId);
        if (d is null) return null;
        return new TrainerDocumentModel
        {
            Id = d.Id,
            TrainerId = d.TrainerId,
            FileName = d.FileName,
            FileSizeBytes = d.FileSizeBytes,
            FileUrl = d.FileUrl,
            DocumentType = (DocumentType)d.DocumentType,
            Status = (DocumentStatus)d.Status,
            RejectionReason = d.RejectionReason,
            ReviewedBy = d.ReviewedBy,
            ReviewedAt = d.ReviewedAt,
            UploadedAt = d.UploadedAt,
        };
    }

    public async Task DeleteAsync(Guid documentId)
    {
        var entity = await context.TrainerDocuments.FindAsync(documentId);
        if (entity is not null)
            context.TrainerDocuments.Remove(entity);
    }

    public async Task<IEnumerable<TrainerDocumentModel>> GetByTrainerIdAsync(Guid trainerId)
    {
        return await context.TrainerDocuments
            .Where(d => d.TrainerId == trainerId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new TrainerDocumentModel
            {
                Id = d.Id,
                TrainerId = d.TrainerId,
                FileName = d.FileName,
                FileSizeBytes = d.FileSizeBytes,
                FileUrl = d.FileUrl,
                DocumentType = (DocumentType)d.DocumentType,
                Status = (DocumentStatus)d.Status,
                RejectionReason = d.RejectionReason,
                ReviewedBy = d.ReviewedBy,
                ReviewedAt = d.ReviewedAt,
                UploadedAt = d.UploadedAt,
            })
            .ToListAsync();
    }

    private IQueryable<AdminDocumentRowModel> AdminBaseQuery() =>
        from d in context.TrainerDocuments.AsNoTracking()
        join u in context.Users.AsNoTracking() on d.TrainerId equals u.Id
        select new AdminDocumentRowModel
        {
            Id = d.Id,
            FileName = d.FileName,
            FileSizeBytes = d.FileSizeBytes,
            FileUrl = d.FileUrl,
            DocumentType = (DocumentType)d.DocumentType,
            Status = (DocumentStatus)d.Status,
            RejectionReason = d.RejectionReason,
            ReviewedBy = d.ReviewedBy,
            ReviewedAt = d.ReviewedAt,
            UploadedAt = d.UploadedAt,
            TrainerId = u.Id,
            TrainerFirstName = u.FirstName,
            TrainerLastName = u.LastName,
            TrainerEmail = u.Email,
            TrainerAvatarUrl = u.AvatarUrl,
        };

    public Task<AdminDocumentRowModel?> GetAdminRowByIdAsync(Guid documentId) =>
        AdminBaseQuery().FirstOrDefaultAsync(r => r.Id == documentId);

    public async Task<(IReadOnlyList<AdminDocumentRowModel> Items, int Total)> SearchPendingAsync(
        string? search, int page, int pageSize)
    {
        var q = AdminBaseQuery().Where(r => r.Status == DocumentStatus.Pending);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(r =>
                EF.Functions.ILike(r.FileName, pattern) ||
                EF.Functions.ILike(r.TrainerFirstName + " " + r.TrainerLastName, pattern) ||
                EF.Functions.ILike(r.TrainerEmail, pattern)
            );
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(r => r.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<int> CountPendingAsync() =>
        context.TrainerDocuments.AsNoTracking()
            .CountAsync(d => d.Status == (short)DocumentStatus.Pending);

    public async Task<bool> ReviewAsync(Guid documentId, DocumentStatus status, Guid reviewerId, string? rejectionReason, DateTime reviewedAt)
    {
        var entity = await context.TrainerDocuments.FindAsync(documentId);
        if (entity is null) return false;

        entity.Status = (short)status;
        entity.ReviewedBy = reviewerId;
        entity.ReviewedAt = reviewedAt;
        entity.RejectionReason = status == DocumentStatus.Rejected ? rejectionReason : null;
        return true;
    }
}
