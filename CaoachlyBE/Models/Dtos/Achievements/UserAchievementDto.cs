namespace CaoachlyBE.Models.Dtos.Achievements;

public class UserAchievementDto
{
    public Guid UserId { get; set; }
    public int AchievementId { get; set; }
    public DateTime EarnedAt { get; set; }
    public AchievementDto Achievement { get; set; } = null!;
}
