using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tickets;

namespace CaoachlyBE.Services.Interfaces;

public interface ISupportTicketService
{
    Task<SupportTicketDto> CreateForBookingAsync(Guid userId, UserRole role, CreateBookingTicketDto dto);
}
