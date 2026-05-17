using CaoachlyBE.Entities;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class ClientInfoRepository(AppDbContext context) : IClientInfoRepository
{
    public async Task AddAsync(ClientInfoModel model)
    {
        var entity = new ClientInfo
        {
            Id = model.Id,
            UserId = model.UserId,
            HeightCm = model.HeightCm,
            WeightKg = model.WeightKg,
            FitnessGoals = model.FitnessGoals
        };
        await context.ClientInfos.AddAsync(entity);
    }

    public async Task<ClientInfoModel?> GetByUserIdAsync(Guid userId)
    {
        var entity = await context.ClientInfos.FirstOrDefaultAsync(c => c.UserId == userId);
        if (entity is null) return null;
        return new ClientInfoModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            HeightCm = entity.HeightCm,
            WeightKg = entity.WeightKg,
            FitnessGoals = entity.FitnessGoals
        };
    }

    public async Task PatchAsync(Guid userId, short? heightCm, decimal? weightKg, string? fitnessGoals)
    {
        var entity = await context.ClientInfos.FirstOrDefaultAsync(c => c.UserId == userId);
        if (entity is null) return;
        if (heightCm.HasValue) entity.HeightCm = heightCm.Value;
        if (weightKg.HasValue) entity.WeightKg = weightKg.Value;
        if (fitnessGoals is not null) entity.FitnessGoals = fitnessGoals;
    }
}
