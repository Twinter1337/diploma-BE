using CaoachlyBE.Entities;
using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class PaymentRepository(AppDbContext context) : IPaymentRepository
{
    public async Task AddAsync(PaymentModel model)
    {
        var entity = new Payment
        {
            Id = model.Id,
            BookingId = model.BookingId,
            Amount = model.Amount,
            Currency = model.Currency,
            PaymentMethod = (short)model.PaymentMethod,
            Status = (short)model.Status,
            TransactionId = model.TransactionId,
            PaidAt = model.PaidAt,
            RefundedAt = model.RefundedAt,
            CreatedAt = model.CreatedAt
        };
        await context.Payments.AddAsync(entity);
    }

    public async Task<PaymentModel?> GetByBookingIdAsync(Guid bookingId)
    {
        var entity = await context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<PaymentModel?> GetByStripeSessionIdAsync(string sessionId)
    {
        var entity = await context.Payments
            .FirstOrDefaultAsync(p => p.TransactionId == sessionId);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task UpdateOnSuccessAsync(Guid bookingId, string paymentIntentId, DateTime paidAt)
    {
        var entity = await context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (entity is null) return;
        entity.Status = (short)PaymentStatus.Paid;
        entity.TransactionId = paymentIntentId;
        entity.PaidAt = paidAt;
    }

    public async Task UpdateRefundAsync(Guid bookingId, DateTime refundedAt)
    {
        var entity = await context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (entity is null) return;
        entity.Status = (short)PaymentStatus.Refunded;
        entity.RefundedAt = refundedAt;
    }

    public async Task UpdateStripeSessionIdAsync(Guid bookingId, string newSessionId)
    {
        var entity = await context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (entity is null) return;
        entity.TransactionId = newSessionId;
    }

    public async Task UpdateAmountAsync(Guid bookingId, decimal amount)
    {
        var entity = await context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (entity is null) return;
        entity.Amount = amount;
    }

    public async Task MarkAsFailedAsync(Guid bookingId)
    {
        var entity = await context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (entity is null) return;
        entity.Status = (short)PaymentStatus.Failed;
    }

    private static PaymentModel MapToModel(Payment entity) => new()
    {
        Id = entity.Id,
        BookingId = entity.BookingId,
        Amount = entity.Amount,
        Currency = entity.Currency,
        PaymentMethod = (PaymentMethod)entity.PaymentMethod,
        Status = (PaymentStatus)entity.Status,
        TransactionId = entity.TransactionId,
        PaidAt = entity.PaidAt,
        RefundedAt = entity.RefundedAt,
        CreatedAt = entity.CreatedAt
    };
}
