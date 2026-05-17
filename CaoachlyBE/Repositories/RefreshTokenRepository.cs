using CaoachlyBE.Entities;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshTokenModel model)
    {
        var entity = new RefreshToken
        {
            Id = model.Id,
            UserId = model.UserId,
            Token = model.Token,
            ExpiresAt = model.ExpiresAt,
            CreatedAt = model.CreatedAt
        };
        await context.RefreshTokens.AddAsync(entity);
    }

    public async Task<RefreshTokenModel?> GetByHashedTokenAsync(string hashedToken)
    {
        var entity = await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hashedToken);

        if (entity is null) return null;

        return new RefreshTokenModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Token = entity.Token,
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt
        };
    }

    public Task DeleteAsync(RefreshTokenModel model)
    {
        var entity = context.RefreshTokens.Local.FirstOrDefault(t => t.Id == model.Id)
            ?? new RefreshToken { Id = model.Id };
        context.RefreshTokens.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task InvalidateAllByUserIdAsync(Guid userId)
    {
        var tokens = await context.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync();
        context.RefreshTokens.RemoveRange(tokens);
    }
}
