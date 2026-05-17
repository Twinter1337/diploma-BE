using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

/// <summary>Joined read-model used by admin queries on support_tickets.</summary>
public class AdminTicketRowModel
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = null!;
    public string Description { get; set; } = null!;
    public TicketStatus Status { get; set; }
    public Guid? RelatedBookingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Linked document (nullable — only set for document-review tickets)
    public Guid? RelatedDocumentId { get; set; }
    public string? DocumentFileName { get; set; }
    public int? DocumentFileSizeBytes { get; set; }
    public string? DocumentFileUrl { get; set; }
    public DocumentType? DocumentType { get; set; }
    public DocumentStatus? DocumentStatus { get; set; }

    // Creator
    public Guid CreatedById { get; set; }
    public string CreatedByFirstName { get; set; } = null!;
    public string CreatedByLastName { get; set; } = null!;
    public string CreatedByEmail { get; set; } = null!;
    public UserRole CreatedByRole { get; set; }
    public string? CreatedByAvatarUrl { get; set; }

    // Assignee (nullable)
    public Guid? AssignedToId { get; set; }
    public string? AssignedToFirstName { get; set; }
    public string? AssignedToLastName { get; set; }
    public string? AssignedToAvatarUrl { get; set; }
}
