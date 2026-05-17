namespace CaoachlyBE.Models.Dtos.Bookings;

public class CancelBookingResponseDto
{
    public Guid BookingId { get; set; }
    public short Status { get; set; }
    public decimal RefundAmount { get; set; }
    public int RefundPercentage { get; set; }
}
