using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class ClientBookingModel
{
    public Guid Id { get; set; }
    public BookingStatus Status { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public string? TrainerAvatarUrl { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SlotFormat Format { get; set; }
    public string? Description { get; set; }
    public string? GymName { get; set; }
    public string? GymAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
