using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Models;
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

    public async Task<IReadOnlyList<int>> CheckAndAwardAsync(Guid userId)
    {
        var unearned = await achievementRepository.GetUnearnedAchievementsAsync(userId);
        if (unearned.Count == 0) return [];

        var stats = await achievementRepository.GetClientStatsAsync(userId);
        var now = DateTime.UtcNow;
        var awarded = new List<int>();

        foreach (var achievement in unearned)
        {
            if (IsMet(achievement.Type, stats))
            {
                await achievementRepository.AwardAsync(userId, achievement.Id, now);
                awarded.Add(achievement.Id);
            }
        }

        return awarded;
    }

    private static bool IsMet(AchievementType type, ClientAchievementStats s) => type switch
    {
        AchievementType.FirstSession           => s.CompletedSessions >= 1,
        AchievementType.FiveSessions           => s.CompletedSessions >= 5,
        AchievementType.TenSessions            => s.CompletedSessions >= 10,
        AchievementType.FiftySessions          => s.CompletedSessions >= 50,
        AchievementType.HundredSessions        => s.CompletedSessions >= 100,

        AchievementType.FirstTrainer           => s.DistinctTrainers >= 1,
        AchievementType.FiveTrainers           => s.DistinctTrainers >= 5,
        AchievementType.TenTrainers            => s.DistinctTrainers >= 10,
        AchievementType.FiftyTrainers          => s.DistinctTrainers >= 50,
        AchievementType.HundredTrainers        => s.DistinctTrainers >= 100,

        AchievementType.FirstSpecialization    => s.DistinctSpecializations >= 1,
        AchievementType.FiveSpecializations    => s.DistinctSpecializations >= 5,
        AchievementType.TenSpecializations     => s.DistinctSpecializations >= 10,
        AchievementType.FiftySpecializations   => s.DistinctSpecializations >= 50,
        AchievementType.HundredSpecializations => s.DistinctSpecializations >= 100,

        AchievementType.FirstCity              => s.DistinctCities >= 1,
        AchievementType.FiveCities             => s.DistinctCities >= 5,
        AchievementType.TenCities              => s.DistinctCities >= 10,
        AchievementType.TwentyFourCities       => s.DistinctCities >= 24,

        AchievementType.TwentyWithOneTrainer   => s.MaxSessionsWithOneTrainer >= 20,
        AchievementType.TwentyAtOneLocation    => s.MaxSessionsAtOneLocation >= 20,

        AchievementType.EarlyBird              => s.HasEarlyMorningSession,
        AchievementType.NightOwl               => s.HasLateEveningSession,
        AchievementType.Marathon               => s.MaxSessionsInOneDay >= 5,
        _                                      => false
    };
}
