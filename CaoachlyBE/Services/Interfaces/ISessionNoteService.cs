using CaoachlyBE.Models.Dtos.Notes;

namespace CaoachlyBE.Services.Interfaces;

public interface ISessionNoteService
{
    Task<SessionNoteDto> CreateAsync(Guid authorId, CreateSessionNoteDto dto);
    Task<IEnumerable<SessionNoteDto>> GetByBookingIdAsync(Guid bookingId, Guid requestingUserId);
    Task<SessionNoteDto> UpdateAsync(Guid noteId, Guid requestingUserId, UpdateSessionNoteDto dto);
    Task DeleteAsync(Guid noteId, Guid requestingUserId);
}
