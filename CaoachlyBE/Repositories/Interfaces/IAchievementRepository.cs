using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IAchievementRepository
{
    Task<IReadOnlyList<UserAchievementListItemModel>> GetUserAchievementsAsync(Guid userId);
    Task<IReadOnlyList<AchievementModel>> GetUnearnedAchievementsAsync(Guid userId);
    Task<ClientAchievementStats> GetClientStatsAsync(Guid clientId);
    Task AwardAsync(Guid userId, int achievementId, DateTime earnedAt);
}
