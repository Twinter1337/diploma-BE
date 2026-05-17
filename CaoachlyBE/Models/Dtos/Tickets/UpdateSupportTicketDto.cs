using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Tickets;

public class UpdateSupportTicketDto
{
    public TicketStatus? Status { get; set; }
    public Guid? AssignedTo { get; set; }
}
