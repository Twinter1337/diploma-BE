using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class TrainerBookingListItemModel
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientAvatarUrl { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SlotFormat Format { get; set; }
    public BookingStatus Status { get; set; }
}
