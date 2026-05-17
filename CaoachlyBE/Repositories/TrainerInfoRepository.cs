using CaoachlyBE.Entities;
using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class TrainerInfoRepository(AppDbContext context) : ITrainerInfoRepository
{
    public async Task AddAsync(TrainerInfoModel model)
    {
        var entity = new TrainerInfo
        {
            Id = model.Id,
            UserId = model.UserId,
            Bio = model.Bio,
            ExperienceYears = model.ExperienceYears,
            VerificationStatus = (short)model.VerificationStatus,
            Rating = model.Rating,
            ReviewsCount = model.ReviewsCount
        };
        await context.TrainerInfos.AddAsync(entity);
    }

    public async Task PatchAsync(Guid userId, string? bio, short? experienceYears)
    {
        var entity = await context.TrainerInfos.FirstOrDefaultAsync(t => t.UserId == userId);
        if (entity is null) return;
        if (bio is not null) entity.Bio = bio;
        if (experienceYears.HasValue) entity.ExperienceYears = experienceYears.Value;
    }

    public async Task<bool> SetVerificationStatusAsync(Guid userId, VerificationStatus status)
    {
        var entity = await context.TrainerInfos.FirstOrDefaultAsync(t => t.UserId == userId);
        if (entity is null) return false;
        entity.VerificationStatus = (short)status;
        return true;
    }

    public async Task<TrainerInfoModel?> GetByUserIdAsync(Guid userId)
    {
        var entity = await context.TrainerInfos.FirstOrDefaultAsync(t => t.UserId == userId);
        if (entity is null) return null;
        return new TrainerInfoModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Bio = entity.Bio,
            ExperienceYears = entity.ExperienceYears,
            VerificationStatus = (VerificationStatus)entity.VerificationStatus,
            Rating = entity.Rating,
            ReviewsCount = entity.ReviewsCount
        };
    }
}
