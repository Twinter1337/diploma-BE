using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface ISessionNoteRepository
{
    Task AddAsync(SessionNoteModel model);
    Task<SessionNoteModel?> GetByIdAsync(Guid id);
    Task<IEnumerable<SessionNoteModel>> GetByBookingIdAsync(Guid bookingId, Guid requestingUserId);
    Task UpdateAsync(Guid id, string? content, bool? isPrivate, DateTime updatedAt);
    Task DeleteAsync(Guid id);
}
