using CaoachlyBE.Entities;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class PasswordResetTokenRepository(AppDbContext context) : IPasswordResetTokenRepository
{
    public async Task<PasswordResetTokenModel?> GetByTokenAsync(string token)
    {
        var entity = await context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == token);

        if (entity is null) return null;

        return new PasswordResetTokenModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Token = entity.Token,
            ExpiresAt = entity.ExpiresAt,
            UsedAt = entity.UsedAt,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task AddAsync(PasswordResetTokenModel model)
    {
        var entity = new PasswordResetToken
        {
            Id = model.Id,
            UserId = model.UserId,
            Token = model.Token,
            ExpiresAt = model.ExpiresAt,
            UsedAt = model.UsedAt,
            CreatedAt = model.CreatedAt
        };
        await context.PasswordResetTokens.AddAsync(entity);
    }

    public async Task MarkUsedAsync(Guid id, DateTime usedAt)
    {
        var entity = await context.PasswordResetTokens.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null) return;
        entity.UsedAt = usedAt;
    }
}
