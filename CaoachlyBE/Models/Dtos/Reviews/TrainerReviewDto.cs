namespace CaoachlyBE.Models.Dtos.Reviews;

public class TrainerReviewDto
{
    public string? AvatarUrl { get; set; }
    public string FullName { get; set; } = null!;
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
