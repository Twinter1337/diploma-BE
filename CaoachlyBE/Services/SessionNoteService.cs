using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Notes;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class SessionNoteService(
    ISessionNoteRepository noteRepository,
    IBookingRepository bookingRepository,
    IScheduleSlotRepository slotRepository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : ISessionNoteService
{
    public async Task<SessionNoteDto> CreateAsync(Guid authorId, CreateSessionNoteDto dto)
    {
        var booking = await bookingRepository.GetByIdAsync(dto.BookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.Status != BookingStatus.Completed)
            throw new InvalidOperationException("Notes can only be added to completed sessions.");

        var slot = await slotRepository.GetByIdAsync(booking.SlotId)
            ?? throw new KeyNotFoundException("Slot not found.");

        if (authorId != booking.ClientId && authorId != slot.TrainerId)
            throw new UnauthorizedAccessException();

        var now = UaTime.Now;
        var model = new SessionNoteModel
        {
            Id = Guid.NewGuid(),
            BookingId = dto.BookingId,
            AuthorId = authorId,
            Content = dto.Content,
            IsPrivate = dto.IsPrivate,
            CreatedAt = now,
            UpdatedAt = now
        };

        await noteRepository.AddAsync(model);
        await unitOfWork.SaveChangesAsync();

        return mapper.Map<SessionNoteDto>(model);
    }

    public async Task<IEnumerable<SessionNoteDto>> GetByBookingIdAsync(Guid bookingId, Guid requestingUserId)
    {
        var notes = await noteRepository.GetByBookingIdAsync(bookingId, requestingUserId);
        return mapper.Map<IEnumerable<SessionNoteDto>>(notes);
    }

    public async Task<SessionNoteDto> UpdateAsync(Guid noteId, Guid requestingUserId, UpdateSessionNoteDto dto)
    {
        var note = await noteRepository.GetByIdAsync(noteId)
            ?? throw new KeyNotFoundException("Note not found.");

        if (note.AuthorId != requestingUserId)
            throw new UnauthorizedAccessException();

        var updatedAt = UaTime.Now;
        await noteRepository.UpdateAsync(noteId, dto.Content, dto.IsPrivate, updatedAt);
        await unitOfWork.SaveChangesAsync();

        var updated = await noteRepository.GetByIdAsync(noteId);
        return mapper.Map<SessionNoteDto>(updated);
    }

    public async Task DeleteAsync(Guid noteId, Guid requestingUserId)
    {
        var note = await noteRepository.GetByIdAsync(noteId)
            ?? throw new KeyNotFoundException("Note not found.");

        if (note.AuthorId != requestingUserId)
            throw new UnauthorizedAccessException();

        await noteRepository.DeleteAsync(noteId);
        await unitOfWork.SaveChangesAsync();
    }
}
