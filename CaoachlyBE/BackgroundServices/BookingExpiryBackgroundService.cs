using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.BackgroundServices;

public class BookingExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ITimeProvider timeProvider,
    ILogger<BookingExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                await ExpireUnpaidBookingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in BookingExpiryBackgroundService.");
            }
        }
    }

    internal async Task ExpireUnpaidBookingsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
        var stripeCheckout = scope.ServiceProvider.GetRequiredService<IStripeCheckoutService>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoff = timeProvider.Now - PaymentWindow;
        var expired = (await bookingRepo.GetExpiredPendingAsync(cutoff)).ToList();
        if (expired.Count == 0) return;

        foreach (var (bookingId, stripeSessionId) in expired)
        {
            try
            {
                var session = await stripeCheckout.GetAsync(stripeSessionId);
                if (session.Status == "open")
                    await stripeCheckout.ExpireAsync(stripeSessionId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not expire Stripe session {SessionId} for booking {BookingId}.", stripeSessionId, bookingId);
            }

            await bookingRepo.CancelAsync(bookingId, CancelledBy.System, "Payment not completed within 15 minutes.");
            await paymentRepo.MarkAsFailedAsync(bookingId);
        }

        await uow.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Expired {Count} unpaid booking(s).", expired.Count);
    }
}
