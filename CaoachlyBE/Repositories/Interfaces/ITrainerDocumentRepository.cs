using CaoachlyBE.Enums;
using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface ITrainerDocumentRepository
{
    Task AddAsync(TrainerDocumentModel model);
    Task<IEnumerable<TrainerDocumentModel>> GetByTrainerIdAsync(Guid trainerId);
    Task<TrainerDocumentModel?> GetByIdAsync(Guid documentId);
    Task DeleteAsync(Guid documentId);

    // Admin queries
    Task<AdminDocumentRowModel?> GetAdminRowByIdAsync(Guid documentId);
    Task<(IReadOnlyList<AdminDocumentRowModel> Items, int Total)> SearchPendingAsync(
        string? search, int page, int pageSize);
    Task<int> CountPendingAsync();

    /// <summary>Marks a document as reviewed. Caller must SaveChanges via UoW.</summary>
    Task<bool> ReviewAsync(Guid documentId, DocumentStatus status, Guid reviewerId, string? rejectionReason, DateTime reviewedAt);
}
