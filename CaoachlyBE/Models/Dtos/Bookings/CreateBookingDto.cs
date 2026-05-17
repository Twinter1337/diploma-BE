namespace CaoachlyBE.Models.Dtos.Bookings;

public class CreateBookingDto
{
    public Guid SlotId { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalAmount { get; set; }
    public int? ReminderMinutes { get; set; }
}
