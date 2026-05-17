using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Bookings;

public class CreateBookingResponseDto
{
    public Guid BookingId { get; set; }
    public string CheckoutUrl { get; set; } = null!;
    public BookingStatus Status { get; set; }
    public bool ServiceFeeApplied { get; set; }
    public decimal TotalAmount { get; set; }
}
