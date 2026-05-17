using CaoachlyBE.Enums;
using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface ISupportTicketRepository
{
    Task<AdminTicketRowModel?> GetRowByIdAsync(Guid id);

    Task<(IReadOnlyList<AdminTicketRowModel> Items, int Total)> SearchAsync(
        Guid? assignedTo,
        bool unassignedOnly,
        string? search,
        int page,
        int pageSize,
        bool? documentLinked = null);

    Task<int> CountByStatusAsync(TicketStatus status);

    /// <summary>Count tickets that are not closed and have no assignee.</summary>
    Task<int> CountUnassignedActiveAsync();

    /// <summary>Inserts a new ticket. Caller must SaveChanges via UoW.</summary>
    Task AddAsync(SupportTicketModel model);

    /// <summary>Transitions the ticket linked to a document. Used by approve/reject.</summary>
    Task<bool> PatchStatusByDocumentAsync(Guid documentId, TicketStatus status, DateTime updatedAt);

    /// <summary>Detaches the document FK and closes the ticket. Used before deleting the document so it survives cascade delete.</summary>
    Task<bool> DetachAndCloseByDocumentAsync(Guid documentId, DateTime updatedAt);

    /// <summary>Updates Status, AssignedTo, UpdatedAt on the tracked entity. Caller must SaveChanges via UoW.</summary>
    Task<bool> PatchAsync(Guid id, TicketStatus? status, Guid? assignedTo, bool unassign, DateTime updatedAt);
}
