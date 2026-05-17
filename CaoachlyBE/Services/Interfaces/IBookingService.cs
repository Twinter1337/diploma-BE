using CaoachlyBE.Models.Dtos.Bookings;

namespace CaoachlyBE.Services.Interfaces;

public interface IBookingService
{
    Task<CreateBookingResponseDto> CreateAsync(
        Guid clientId,
        string clientEmail,
        string clientName,
        CreateBookingDto dto);

    Task ConfirmFromWebhookAsync(string stripeSessionId, string paymentIntentId);

    Task<IEnumerable<UserBookingListItemDto>> GetByClientIdAsync(Guid clientId);
    Task<IEnumerable<BookingHistoryItemDto>> GetHistoryByClientIdAsync(Guid clientId);
    Task<CancelBookingResponseDto> CancelAsync(Guid bookingId, Guid clientId);
    Task<CreateBookingResponseDto> RetryPaymentAsync(Guid bookingId, Guid clientId, string clientEmail);
}
