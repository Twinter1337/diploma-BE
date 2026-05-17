namespace CaoachlyBE.Models.Dtos.Achievements;

public class UserAchievementsListDto
{
    public IEnumerable<UserAchievementListItemDto> Achievements { get; set; } = [];
    public int TotalCount { get; set; }
    public int EarnedCount { get; set; }
}
