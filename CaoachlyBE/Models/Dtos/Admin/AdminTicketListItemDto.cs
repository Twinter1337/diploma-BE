namespace CaoachlyBE.Models.Dtos.Admin;

public class AdminTicketListItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!; // "request" | "document"
    public short Status { get; set; }
    public short Priority { get; set; } = 1; // 0=high, 1=normal, 2=low (stub: always normal)
    public string Subject { get; set; } = null!;
    public string? DocType { get; set; } // "certificate" | "diploma" | "license" | "other"
    public AdminTicketCreatorDto CreatedBy { get; set; } = null!;
    public AdminUserSummaryDto? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
}
