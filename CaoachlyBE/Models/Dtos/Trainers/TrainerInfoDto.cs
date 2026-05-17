using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Trainers;

public class TrainerInfoDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Bio { get; set; }
    public short ExperienceYears { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
}
