using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Bookings;

public class BookingReviewDto
{
    public short Rating { get; set; }
    public string? Comment { get; set; }
}

public class BookingHistoryItemDto
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public Guid TrainerId { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public string? TrainerAvatarUrl { get; set; }
    public decimal Price { get; set; }
    public BookingStatus BookingStatus { get; set; }
    public BookingReviewDto? Review { get; set; }
}
