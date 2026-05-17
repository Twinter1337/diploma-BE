using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IPaymentRepository
{
    Task AddAsync(PaymentModel model);
    Task<PaymentModel?> GetByBookingIdAsync(Guid bookingId);
    Task<PaymentModel?> GetByStripeSessionIdAsync(string sessionId);
    Task UpdateOnSuccessAsync(Guid bookingId, string paymentIntentId, DateTime paidAt);
    Task UpdateRefundAsync(Guid bookingId, DateTime refundedAt);
    Task UpdateStripeSessionIdAsync(Guid bookingId, string newSessionId);
    Task UpdateAmountAsync(Guid bookingId, decimal amount);
    Task MarkAsFailedAsync(Guid bookingId);
}
