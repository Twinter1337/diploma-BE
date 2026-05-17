namespace CaoachlyBE.Models.Dtos.Admin;

public class DocumentReviewResultDto
{
    public Guid DocumentId { get; set; }
    public short Status { get; set; }       // document status: 1=approved, 2=rejected
    public short TicketStatus { get; set; } // derived ticket status: 2=resolved, 3=closed
    public DateTime ReviewedAt { get; set; }
}
