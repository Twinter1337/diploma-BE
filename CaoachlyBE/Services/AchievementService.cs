using AutoMapper;
using CaoachlyBE.Models.Dtos.Achievements;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class AchievementService(IAchievementRepository achievementRepository, IMapper mapper) : IAchievementService
{
    public async Task<UserAchievementsListDto> GetUserAchievementsAsync(Guid userId)
    {
        var items = await achievementRepository.GetUserAchievementsAsync(userId);
        return new UserAchievementsListDto
        {
            Achievements = items.Select(mapper.Map<UserAchievementListItemDto>),
            TotalCount = items.Count,
            EarnedCount = items.Count(x => x.IsEarned)
        };
    }

    public async Task<IEnumerable<AchievementDto>> GetUnearnedAchievementsAsync(Guid userId)
    {
        var items = await achievementRepository.GetUnearnedAchievementsAsync(userId);
        return items.Select(mapper.Map<AchievementDto>);
    }
}
