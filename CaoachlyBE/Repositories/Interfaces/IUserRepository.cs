using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Stats;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email);
    Task AddAsync(UserModel model);
    Task<UserModel?> GetByIdAsync(Guid id);
    Task<UserModel?> GetByEmailAsync(string email);
    Task PatchAsync(Guid id, string? firstName, string? lastName, string? avatarUrl, string? city, string? phone, short? gender, DateOnly? birthDate, DateTime updatedAt);
    Task ReplaceTagsByCategoryAsync(Guid userId, TagCategory category, IEnumerable<int> tagIds);
    Task<IEnumerable<TagModel>> GetTagsByCategoryAsync(Guid userId, TagCategory category);
    Task<IEnumerable<TrainerSummaryModel>> SearchTrainersAsync(TrainerSearchFilter filter);
    Task<TrainerPublicProfileModel?> GetTrainerPublicProfileAsync(Guid trainerId);
    Task<PlatformStatsDto> GetPlatformStatsAsync();
    Task PatchIdentityAsync(Guid id, string? firstName, string? lastName, string? email, DateTime updatedAt);
    Task SoftDeleteAndAnonymizeAsync(Guid userId, DateTime updatedAt);
    Task ClearAvatarUrlAsync(Guid userId, DateTime updatedAt);
    Task<IEnumerable<UserModel>> GetByRoleAsync(UserRole role);
    Task UpdatePasswordHashAsync(Guid id, string passwordHash, DateTime updatedAt);
}
