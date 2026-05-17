using CaoachlyBE.Entities;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class SessionNoteRepository(AppDbContext context) : ISessionNoteRepository
{
    public async Task AddAsync(SessionNoteModel model)
    {
        var entity = new SessionNote
        {
            Id = model.Id,
            BookingId = model.BookingId,
            AuthorId = model.AuthorId,
            Content = model.Content,
            IsPrivate = model.IsPrivate,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
        await context.SessionNotes.AddAsync(entity);
    }

    public async Task<SessionNoteModel?> GetByIdAsync(Guid id)
    {
        var entity = await context.SessionNotes.FirstOrDefaultAsync(n => n.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<IEnumerable<SessionNoteModel>> GetByBookingIdAsync(Guid bookingId, Guid requestingUserId)
    {
        return await context.SessionNotes
            .Where(n => n.BookingId == bookingId && (!n.IsPrivate || n.AuthorId == requestingUserId))
            .OrderBy(n => n.CreatedAt)
            .Select(n => new SessionNoteModel
            {
                Id = n.Id,
                BookingId = n.BookingId,
                AuthorId = n.AuthorId,
                Content = n.Content,
                IsPrivate = n.IsPrivate,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task UpdateAsync(Guid id, string? content, bool? isPrivate, DateTime updatedAt)
    {
        var entity = await context.SessionNotes.FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Note not found.");

        if (content is not null) entity.Content = content;
        if (isPrivate.HasValue) entity.IsPrivate = isPrivate.Value;
        entity.UpdatedAt = updatedAt;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await context.SessionNotes.FirstOrDefaultAsync(n => n.Id == id);
        if (entity is not null) context.SessionNotes.Remove(entity);
    }

    private static SessionNoteModel MapToModel(SessionNote entity) => new()
    {
        Id = entity.Id,
        BookingId = entity.BookingId,
        AuthorId = entity.AuthorId,
        Content = entity.Content,
        IsPrivate = entity.IsPrivate,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
