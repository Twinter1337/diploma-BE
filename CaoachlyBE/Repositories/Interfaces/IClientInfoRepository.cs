using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IClientInfoRepository
{
    Task AddAsync(ClientInfoModel model);
    Task<ClientInfoModel?> GetByUserIdAsync(Guid userId);
    Task PatchAsync(Guid userId, short? heightCm, decimal? weightKg, string? fitnessGoals);
}
