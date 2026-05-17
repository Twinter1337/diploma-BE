using CaoachlyBE.Entities;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.BackgroundServices;

public class NotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in NotificationBackgroundService.");
            }
        }
    }

    internal async Task ProcessRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var reminders = (await bookingRepo.GetDueForReminderAsync()).ToList();
        if (reminders.Count == 0) return;

        var pendingEmails = new List<(string Email, SessionReminderData Data)>();

        foreach (var r in reminders)
        {
            var trainerFullName = $"{r.TrainerFirstName} {r.TrainerLastName}";
            var clientFullName = $"{r.ClientFirstName} {r.ClientLastName}";

            var clientTitle = "Session Reminder";
            var clientBody = $"Your session with {trainerFullName} starts at {r.StartTime:dd MMM yyyy, HH:mm}.";
            var trainerTitle = "Upcoming Session";
            var trainerBody = $"You have a session with {clientFullName} starting at {r.StartTime:dd MMM yyyy, HH:mm}.";

            await notificationRepo.AddAsync(new Notification
            {
                UserId = r.ClientId,
                BookingId = r.BookingId,
                Type = (short)NotificationType.SessionReminder,
                Title = clientTitle,
                Body = clientBody
            });

            await notificationRepo.AddAsync(new Notification
            {
                UserId = r.TrainerId,
                BookingId = r.BookingId,
                Type = (short)NotificationType.SessionReminder,
                Title = trainerTitle,
                Body = trainerBody
            });

            pendingEmails.Add((r.ClientEmail, new SessionReminderData
            {
                RecipientFirstName = r.ClientFirstName,
                TrainerFullName = trainerFullName,
                ClientFullName = clientFullName,
                StartTime = r.StartTime,
                IsTrainer = false
            }));

            pendingEmails.Add((r.TrainerEmail, new SessionReminderData
            {
                RecipientFirstName = r.TrainerFirstName,
                TrainerFullName = trainerFullName,
                ClientFullName = clientFullName,
                StartTime = r.StartTime,
                IsTrainer = true
            }));
        }

        await uow.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Queued {Count} session reminder(s).", reminders.Count);

        foreach (var (email, data) in pendingEmails)
            _ = emailService.SendSessionReminderAsync(email, data);
    }
}
