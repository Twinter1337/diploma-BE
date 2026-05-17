using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Bookings;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using Stripe.Checkout;

namespace CaoachlyBE.Services;

public class BookingService(
    IBookingRepository bookingRepo,
    IPaymentRepository paymentRepo,
    IScheduleSlotRepository slotRepo,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    IConfiguration configuration,
    IMapper mapper,
    IStripeCheckoutService stripeCheckout,
    IStripeRefundService stripeRefund,
    ITimeProvider timeProvider) : IBookingService
{
    public async Task<CreateBookingResponseDto> CreateAsync(
        Guid clientId,
        string clientEmail,
        string clientName,
        CreateBookingDto dto)
    {
        var slot = await slotRepo.GetByIdAsync(dto.SlotId)
            ?? throw new KeyNotFoundException($"Slot {dto.SlotId} not found.");

        if (slot.Status != SlotStatus.Available)
            throw new InvalidOperationException("This slot is no longer available.");

        var hasConflict = await bookingRepo.HasConflictAsync(clientId, slot.StartTime, slot.EndTime);
        if (hasConflict)
            throw new InvalidOperationException("You already have an active booking that overlaps with this time slot.");

        var now = timeProvider.Now;
        var bookingId = Guid.NewGuid();

        var booking = new BookingModel
        {
            Id = bookingId,
            SlotId = dto.SlotId,
            ClientId = clientId,
            Status = BookingStatus.Pending,
            ReminderMinutes = dto.ReminderMinutes,
            CreatedAt = now,
            UpdatedAt = now
        };

        var trainer = await userRepo.GetByIdAsync(slot.TrainerId);
        var trainerName = trainer is not null ? $"{trainer.FirstName} {trainer.LastName}" : "Trainer";

        var sessionOptions = new SessionCreateOptions
        {
            CustomerEmail = clientEmail,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "uah",
                        UnitAmount = (long)(dto.TotalAmount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Training session with {trainerName}",
                            Description = slot.Description
                                ?? $"{slot.StartTime:dd MMM yyyy, HH:mm} — {slot.EndTime:HH:mm}"
                        }
                    },
                    Quantity = 1
                }
            ],
            Mode = "payment",
            SuccessUrl = configuration["Stripe:SuccessUrl"]!,
            CancelUrl = configuration["Stripe:CancelUrl"]!,
            Metadata = new Dictionary<string, string>
            {
                ["bookingId"] = bookingId.ToString()
            }
        };

        var session = await stripeCheckout.CreateAsync(sessionOptions);

        var payment = new PaymentModel
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Amount = dto.TotalAmount,
            Currency = "UAH",
            PaymentMethod = PaymentMethod.Online,
            Status = PaymentStatus.Pending,
            TransactionId = session.Id,
            CreatedAt = now
        };

        await bookingRepo.AddAsync(booking);
        await paymentRepo.AddAsync(payment);
        await unitOfWork.SaveChangesAsync();

        return new CreateBookingResponseDto
        {
            BookingId = bookingId,
            CheckoutUrl = session.Url,
            Status = BookingStatus.Pending,
            ServiceFeeApplied = false,
            TotalAmount = dto.TotalAmount
        };
    }

    public async Task<IEnumerable<UserBookingListItemDto>> GetByClientIdAsync(Guid clientId)
    {
        var models = await bookingRepo.GetByClientIdAsync(clientId);
        return mapper.Map<IEnumerable<UserBookingListItemDto>>(models);
    }

    public async Task<IEnumerable<BookingHistoryItemDto>> GetHistoryByClientIdAsync(Guid clientId)
    {
        var models = await bookingRepo.GetHistoryByClientIdAsync(clientId);
        return mapper.Map<IEnumerable<BookingHistoryItemDto>>(models);
    }

    public async Task<CancelBookingResponseDto> CancelAsync(Guid bookingId, Guid clientId)
    {
        var booking = await bookingRepo.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.ClientId != clientId)
            throw new UnauthorizedAccessException();

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Booking is already cancelled or completed.");

        var slot = await slotRepo.GetByIdAsync(booking.SlotId);
        var now = timeProvider.Now;
        var timeUntilSession = slot is not null ? slot.StartTime - now : TimeSpan.FromHours(25);
        var refundPercentage = timeUntilSession.TotalHours > 24 ? 100 : 50;

        var payment = await paymentRepo.GetByBookingIdAsync(bookingId);
        var refundAmount = 0m;

        if (payment?.Status == PaymentStatus.Paid)
        {
            refundAmount = payment.Amount * refundPercentage / 100;
            var refundOptions = new Stripe.RefundCreateOptions
            {
                PaymentIntent = payment.TransactionId,
                Amount = (long)(refundAmount * 100)
            };
            await stripeRefund.CreateAsync(refundOptions);
            await paymentRepo.UpdateRefundAsync(bookingId, now);
        }

        await bookingRepo.CancelAsync(bookingId, CancelledBy.Client, null);
        await unitOfWork.SaveChangesAsync();

        if (payment?.Status == PaymentStatus.Paid && refundAmount > 0)
        {
            var client = await userRepo.GetByIdAsync(clientId);
            var trainer = slot is not null ? await userRepo.GetByIdAsync(slot.TrainerId) : null;
            var clientEmail = client?.Email;

            if (!string.IsNullOrEmpty(clientEmail))
            {
                var notificationData = new RefundNotificationData(
                    ClientFirstName: client?.FirstName ?? "Client",
                    TrainerName: trainer is not null ? $"{trainer.FirstName} {trainer.LastName}" : "Trainer",
                    RefundAmount: refundAmount,
                    Currency: payment.Currency,
                    RefundPercentage: refundPercentage,
                    SessionStartTime: slot!.StartTime,
                    SessionEndTime: slot.EndTime,
                    CancelledAt: now
                );
                _ = emailService.SendRefundNotificationAsync(clientEmail, notificationData);
            }
        }

        return new CancelBookingResponseDto
        {
            BookingId = bookingId,
            Status = (short)BookingStatus.Cancelled,
            RefundAmount = refundAmount,
            RefundPercentage = refundPercentage
        };
    }

    public async Task<CreateBookingResponseDto> RetryPaymentAsync(Guid bookingId, Guid clientId, string clientEmail)
    {
        var booking = await bookingRepo.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.ClientId != clientId)
            throw new UnauthorizedAccessException();

        if (booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException("Only pending bookings can be retried.");

        var payment = await paymentRepo.GetByBookingIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Payment record not found.");

        var existingSession = await stripeCheckout.GetAsync(payment.TransactionId!);

        if (existingSession.Status == "open")
        {
            return new CreateBookingResponseDto
            {
                BookingId = bookingId,
                CheckoutUrl = existingSession.Url,
                Status = BookingStatus.Pending,
                ServiceFeeApplied = false,
                TotalAmount = payment.Amount
            };
        }

        var slot = await slotRepo.GetByIdAsync(booking.SlotId)
            ?? throw new KeyNotFoundException("Slot not found.");

        var trainer = await userRepo.GetByIdAsync(slot.TrainerId);
        var trainerName = trainer is not null ? $"{trainer.FirstName} {trainer.LastName}" : "Trainer";

        var reservationWindowMinutes = configuration.GetValue<int>("Booking:ReservationWindowMinutes", 15);
        var lateFeePercent = configuration.GetValue<int>("Booking:LateFeePercent", 20);
        var isLate = (timeProvider.Now - booking.CreatedAt).TotalMinutes > reservationWindowMinutes;
        var baseAmount = payment.Amount;
        var totalAmount = isLate
            ? Math.Round(baseAmount * (1 + lateFeePercent / 100m), 2)
            : baseAmount;

        var sessionOptions = new SessionCreateOptions
        {
            CustomerEmail = clientEmail,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "uah",
                        UnitAmount = (long)(totalAmount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Training session with {trainerName}"
                                + (isLate ? $" (includes {lateFeePercent}% late payment fee)" : string.Empty),
                            Description = slot.Description
                                ?? $"{slot.StartTime:dd MMM yyyy, HH:mm} — {slot.EndTime:HH:mm}"
                        }
                    },
                    Quantity = 1
                }
            ],
            Mode = "payment",
            SuccessUrl = configuration["Stripe:SuccessUrl"]!,
            CancelUrl = configuration["Stripe:CancelUrl"]!,
            Metadata = new Dictionary<string, string>
            {
                ["bookingId"] = bookingId.ToString()
            }
        };

        var newSession = await stripeCheckout.CreateAsync(sessionOptions);
        await paymentRepo.UpdateStripeSessionIdAsync(bookingId, newSession.Id);

        if (isLate)
            await paymentRepo.UpdateAmountAsync(bookingId, totalAmount);

        await unitOfWork.SaveChangesAsync();

        return new CreateBookingResponseDto
        {
            BookingId = bookingId,
            CheckoutUrl = newSession.Url,
            Status = BookingStatus.Pending,
            ServiceFeeApplied = isLate,
            TotalAmount = totalAmount
        };
    }

    public async Task ConfirmFromWebhookAsync(string stripeSessionId, string paymentIntentId)
    {
        var payment = await paymentRepo.GetByStripeSessionIdAsync(stripeSessionId)
            ?? throw new KeyNotFoundException($"No payment found for Stripe session {stripeSessionId}.");

        var booking = await bookingRepo.GetByIdAsync(payment.BookingId)
            ?? throw new KeyNotFoundException($"Booking {payment.BookingId} not found.");

        var slot = await slotRepo.GetByIdAsync(booking.SlotId)
            ?? throw new KeyNotFoundException($"Slot {booking.SlotId} not found.");

        var trainer = await userRepo.GetByIdAsync(slot.TrainerId);
        var client = await userRepo.GetByIdAsync(booking.ClientId);

        var trainerName = trainer is not null ? $"{trainer.FirstName} {trainer.LastName}" : "Trainer";
        var clientName = client is not null ? $"{client.FirstName} {client.LastName}" : "Client";
        var clientEmail = client?.Email ?? string.Empty;

        var now = timeProvider.Now;

        await bookingRepo.UpdateStatusAsync(booking.Id, BookingStatus.Confirmed);
        await paymentRepo.UpdateOnSuccessAsync(booking.Id, paymentIntentId, now);
        await unitOfWork.SaveChangesAsync();

        if (!string.IsNullOrEmpty(clientEmail))
        {
            var receiptData = new ReceiptData(
                ClientName: clientName,
                TrainerName: trainerName,
                Amount: payment.Amount,
                Currency: payment.Currency,
                SessionStartTime: slot.StartTime,
                SessionEndTime: slot.EndTime,
                SessionFormat: slot.Format == SlotFormat.Online ? "Online" : "Offline",
                PaymentIntentId: paymentIntentId,
                PaidAt: now
            );

            await emailService.SendBookingReceiptAsync(clientEmail, receiptData);
        }
    }
}
