using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Tickets;

public class CreateSupportTicketDto
{
    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = null!;

    public Guid? RelatedBookingId { get; set; }
}
