using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshTokenModel model);
    Task<RefreshTokenModel?> GetByHashedTokenAsync(string hashedToken);
    Task DeleteAsync(RefreshTokenModel model);
    Task InvalidateAllByUserIdAsync(Guid userId);
}
