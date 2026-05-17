using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IAchievementRepository
{
    Task<IReadOnlyList<UserAchievementListItemModel>> GetUserAchievementsAsync(Guid userId);
    Task<IReadOnlyList<AchievementModel>> GetUnearnedAchievementsAsync(Guid userId);
}
