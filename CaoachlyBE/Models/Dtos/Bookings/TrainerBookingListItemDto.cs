using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Bookings;

public class TrainerBookingListItemDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientAvatarUrl { get; set; }
    public DateTime StartDateTime { get; set; }
    public int DurationInMinutes { get; set; }
    public SlotFormat Format { get; set; }
    public BookingStatus Status { get; set; }
}
