namespace CaoachlyBE.Models.Dtos.Reviews;

public class CreateReviewResponseDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
