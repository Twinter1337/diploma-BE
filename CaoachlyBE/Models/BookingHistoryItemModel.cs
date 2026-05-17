using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class BookingHistoryItemModel
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public Guid TrainerId { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public string? TrainerAvatarUrl { get; set; }
    public decimal Price { get; set; }
    public BookingStatus Status { get; set; }
    public short? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
}
