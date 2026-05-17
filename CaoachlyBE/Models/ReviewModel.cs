namespace CaoachlyBE.Models;

public class ReviewModel
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid ClientId { get; set; }
    public Guid TrainerId { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
