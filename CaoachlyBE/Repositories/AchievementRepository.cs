using CaoachlyBE.Entities;
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

    public async Task<ClientAchievementStats> GetClientStatsAsync(Guid clientId)
    {
        var bookings = await context.Bookings
            .Where(b => b.ClientId == clientId && b.Status == (short)BookingStatus.Completed)
            .Include(b => b.Slot)
                .ThenInclude(s => s.Trainer)
                    .ThenInclude(t => t.Tags)
            .ToListAsync();

        if (bookings.Count == 0)
            return new ClientAchievementStats(0, 0, 0, 0, 0, 0, false, false, 0);

        var distinctSpecializations = bookings
            .SelectMany(b => b.Slot.Trainer.Tags)
            .Where(t => t.Category == (short)TagCategory.Specialization)
            .Select(t => t.Id)
            .Distinct()
            .Count();

        var distinctCities = bookings
            .Select(b => b.Slot.Trainer.City)
            .Where(c => c != null)
            .Distinct()
            .Count();

        var maxWithOneTrainer = bookings
            .GroupBy(b => b.Slot.TrainerId)
            .Max(g => g.Count());

        var locationGroups = bookings
            .Where(b => b.Slot.GymName != null)
            .GroupBy(b => b.Slot.GymName)
            .ToList();

        var maxAtOneLocation = locationGroups.Count > 0
            ? locationGroups.Max(g => g.Count())
            : 0;

        var maxInOneDay = bookings
            .GroupBy(b => b.Slot.StartTime.Date)
            .Max(g => g.Count());

        return new ClientAchievementStats(
            CompletedSessions: bookings.Count,
            DistinctTrainers: bookings.Select(b => b.Slot.TrainerId).Distinct().Count(),
            DistinctSpecializations: distinctSpecializations,
            DistinctCities: distinctCities,
            MaxSessionsWithOneTrainer: maxWithOneTrainer,
            MaxSessionsAtOneLocation: maxAtOneLocation,
            HasEarlyMorningSession: bookings.Any(b => b.Slot.StartTime.Hour < 8),
            HasLateEveningSession: bookings.Any(b => b.Slot.StartTime.Hour >= 20),
            MaxSessionsInOneDay: maxInOneDay
        );
    }

    public Task AwardAsync(Guid userId, int achievementId, DateTime earnedAt)
    {
        context.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            AchievementId = achievementId,
            EarnedAt = earnedAt
        });
        return Task.CompletedTask;
    }
}
