using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Bookings;

public class UserBookingListItemDto
{
    public Guid Id { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public SlotFormat Format { get; set; }
    public string? TrainerAvatarUrl { get; set; }
    public string? Description { get; set; }
    public string? GymName { get; set; }
    public string? GymAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
