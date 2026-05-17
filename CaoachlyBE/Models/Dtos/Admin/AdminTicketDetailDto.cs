namespace CaoachlyBE.Models.Dtos.Admin;

public class AdminTicketDetailDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!; // "request" | "document"
    public short Status { get; set; }
    public short Priority { get; set; } = 1;
    public string Subject { get; set; } = null!;
    public string? Description { get; set; }
    public AdminTicketCreatorDto CreatedBy { get; set; } = null!;
    public AdminUserSummaryDto? AssignedTo { get; set; }
    public Guid? RelatedBookingId { get; set; }
    public Guid? RelatedTrainerId { get; set; }
    public AdminTicketDocumentDto? Document { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
