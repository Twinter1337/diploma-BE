using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetTokenModel?> GetByTokenAsync(string token);
    Task AddAsync(PasswordResetTokenModel model);
    Task MarkUsedAsync(Guid id, DateTime usedAt);
}
