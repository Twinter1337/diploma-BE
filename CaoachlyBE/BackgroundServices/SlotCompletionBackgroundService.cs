using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.BackgroundServices;

public class SlotCompletionBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SlotCompletionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                await ProcessExpiredSlotsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in SlotCompletionBackgroundService.");
            }
        }
    }

    internal async Task ProcessExpiredSlotsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var slotRepo = scope.ServiceProvider.GetRequiredService<IScheduleSlotRepository>();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var achievementService = scope.ServiceProvider.GetRequiredService<IAchievementService>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var expiredSlotIds = (await slotRepo.GetExpiredActiveAsync()).ToList();
        if (expiredSlotIds.Count == 0) return;

        var pendingEmails = new List<(string Email, ReviewRequestData Data)>();
        var completedClientIds = new List<Guid>();

        foreach (var slotId in expiredSlotIds)
        {
            await slotRepo.UpdateStatusAsync(slotId, SlotStatus.Completed);

            var bookings = await bookingRepo.GetConfirmedWithClientBySlotIdAsync(slotId);
            foreach (var booking in bookings)
            {
                await bookingRepo.UpdateStatusAsync(booking.BookingId, BookingStatus.Completed);
                completedClientIds.Add(booking.ClientId);
                pendingEmails.Add((booking.ClientEmail, new ReviewRequestData(
                    ClientFirstName: booking.ClientFirstName,
                    TrainerFullName: booking.TrainerFullName,
                    SessionStartTime: booking.SlotStartTime,
                    SessionEndTime: booking.SlotEndTime,
                    BookingId: booking.BookingId
                )));
            }
        }

        await uow.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Marked {SlotCount} slot(s) as completed.", expiredSlotIds.Count);

        foreach (var clientId in completedClientIds.Distinct())
            await achievementService.CheckAndAwardAsync(clientId);

        await uow.SaveChangesAsync(cancellationToken);

        foreach (var (email, data) in pendingEmails)
            _ = emailService.SendReviewRequestAsync(email, data);
    }
}
