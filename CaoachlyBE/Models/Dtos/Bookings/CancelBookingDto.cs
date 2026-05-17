using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Bookings;

public class CancelBookingDto
{
    [MaxLength(500)]
    public string? CancellationReason { get; set; }
}
