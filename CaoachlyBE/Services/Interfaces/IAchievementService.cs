using CaoachlyBE.Models.Dtos.Achievements;

namespace CaoachlyBE.Services.Interfaces;

public interface IAchievementService
{
    Task<UserAchievementsListDto> GetUserAchievementsAsync(Guid userId);
    Task<IEnumerable<AchievementDto>> GetUnearnedAchievementsAsync(Guid userId);
}
