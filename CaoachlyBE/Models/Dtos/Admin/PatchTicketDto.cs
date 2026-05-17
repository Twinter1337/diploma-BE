using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Admin;

public class PatchTicketDto
{
    public TicketStatus? Status { get; set; }
    public Guid? AssignedTo { get; set; }
    /// <summary>If true, clears the AssignedTo field. AssignedTo value is ignored.</summary>
    public bool Unassign { get; set; } = false;
}
