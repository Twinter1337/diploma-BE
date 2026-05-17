using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Tickets;

public class SupportTicketDto
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public string Subject { get; set; } = null!;
    public string Description { get; set; } = null!;
    public TicketStatus Status { get; set; }
    public Guid? RelatedBookingId { get; set; }
    public Guid? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
