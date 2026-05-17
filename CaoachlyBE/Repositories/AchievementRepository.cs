using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class AchievementRepository(AppDbContext context) : IAchievementRepository
{
    public async Task<IReadOnlyList<UserAchievementListItemModel>> GetUserAchievementsAsync(Guid userId)
    {
        return await context.Achievements
            .GroupJoin(
                context.UserAchievements.Where(ua => ua.UserId == userId),
                a => a.Id,
                ua => ua.AchievementId,
                (a, uas) => new { Achievement = a, UserAchievements = uas })
            .SelectMany(
                x => x.UserAchievements.DefaultIfEmpty(),
                (x, ua) => new UserAchievementListItemModel
                {
                    Id = x.Achievement.Id,
                    Type = (AchievementType)x.Achievement.Type,
                    Title = x.Achievement.Title,
                    Description = x.Achievement.Description,
                    IconUrl = x.Achievement.IconUrl,
                    IsEarned = ua != null,
                    EarnedAt = ua != null ? ua.EarnedAt : (DateTime?)null
                })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AchievementModel>> GetUnearnedAchievementsAsync(Guid userId)
    {
        var earnedIds = context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AchievementId);

        return await context.Achievements
            .Where(a => !earnedIds.Contains(a.Id))
            .Select(a => new AchievementModel
            {
                Id = a.Id,
                Type = (AchievementType)a.Type,
                Title = a.Title,
                Description = a.Description,
                IconUrl = a.IconUrl
            })
            .ToListAsync();
    }
}
