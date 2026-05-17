using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class BookingModel
{
    public Guid Id { get; set; }
    public Guid SlotId { get; set; }
    public Guid ClientId { get; set; }
    public BookingStatus Status { get; set; }
    public string? CancellationReason { get; set; }
    public CancelledBy? CancelledBy { get; set; }
    public int? ReminderMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
