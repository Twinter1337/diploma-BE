using CaoachlyBE.Enums;
using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface ITrainerInfoRepository
{
    Task AddAsync(TrainerInfoModel model);
    Task PatchAsync(Guid userId, string? bio, short? experienceYears);
    Task<TrainerInfoModel?> GetByUserIdAsync(Guid userId);
    Task<bool> SetVerificationStatusAsync(Guid userId, VerificationStatus status);
}
