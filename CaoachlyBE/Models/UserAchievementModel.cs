namespace CaoachlyBE.Models;

public class UserAchievementModel
{
    public Guid UserId { get; set; }
    public int AchievementId { get; set; }
    public DateTime EarnedAt { get; set; }
}
