using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Tickets;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class SupportTicketService(
    ISupportTicketRepository supportTicketRepository,
    IBookingRepository bookingRepository,
    IScheduleSlotRepository scheduleSlotRepository,
    IUnitOfWork unitOfWork) : ISupportTicketService
{
    public async Task<SupportTicketDto> CreateForBookingAsync(Guid userId, UserRole role, CreateBookingTicketDto dto)
    {
        var booking = await bookingRepository.GetByIdAsync(dto.BookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (role == UserRole.Client)
        {
            if (booking.ClientId != userId)
                throw new UnauthorizedAccessException();
        }
        else if (role == UserRole.Trainer)
        {
            var slot = await scheduleSlotRepository.GetByIdAsync(booking.SlotId)
                ?? throw new KeyNotFoundException("Booking not found.");
            if (slot.TrainerId != userId)
                throw new UnauthorizedAccessException();
        }
        else
        {
            throw new UnauthorizedAccessException();
        }

        var now = UaTime.Now;
        var model = new SupportTicketModel
        {
            Id = Guid.NewGuid(),
            CreatedBy = userId,
            Subject = dto.Subject,
            Description = dto.Description,
            Status = TicketStatus.Open,
            RelatedBookingId = booking.Id,
            RelatedDocumentId = null,
            AssignedTo = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await supportTicketRepository.AddAsync(model);
        await unitOfWork.SaveChangesAsync();

        return new SupportTicketDto
        {
            Id = model.Id,
            CreatedBy = model.CreatedBy,
            Subject = model.Subject,
            Description = model.Description,
            Status = model.Status,
            RelatedBookingId = model.RelatedBookingId,
            AssignedTo = model.AssignedTo,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
    }
}
