using CaoachlyBE.BackgroundServices;
using CaoachlyBE.Entities;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CaoachlyBE.Tests.BackgroundServices;

public class SessionReminderTests
{
    // ── test-data builders ────────────────────────────────────────────────────
    private static SessionReminderInfo Reminder(
        string clientEmail   = "client@test.com",
        string trainerEmail  = "trainer@test.com",
        string clientFirst   = "Alice",
        string clientLast    = "Smith",
        string trainerFirst  = "Bob",
        string trainerLast   = "Jones") => new()
    {
        BookingId       = Guid.NewGuid(),
        ClientId        = Guid.NewGuid(),
        ClientEmail     = clientEmail,
        ClientFirstName = clientFirst,
        ClientLastName  = clientLast,
        TrainerId       = Guid.NewGuid(),
        TrainerEmail    = trainerEmail,
        TrainerFirstName = trainerFirst,
        TrainerLastName  = trainerLast,
        StartTime       = new DateTime(2026, 5, 18, 10, 0, 0),
    };

    // ── service factory ───────────────────────────────────────────────────────
    private static (NotificationBackgroundService Sut, Mock<IServiceScopeFactory> ScopeFactory) CreateSut(
        Mock<IBookingRepository>?      bookingRepo      = null,
        Mock<INotificationRepository>? notificationRepo = null,
        Mock<IEmailService>?           emailService     = null,
        Mock<IUnitOfWork>?             unitOfWork       = null)
    {
        bookingRepo      ??= TestMocks.BookingRepo();
        notificationRepo ??= TestMocks.NotificationRepo();
        emailService     ??= TestMocks.EmailService();
        unitOfWork       ??= TestMocks.UnitOfWork();

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IBookingRepository))).Returns(bookingRepo.Object);
        provider.Setup(p => p.GetService(typeof(INotificationRepository))).Returns(notificationRepo.Object);
        provider.Setup(p => p.GetService(typeof(IEmailService))).Returns(emailService.Object);
        provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(unitOfWork.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var sut = new NotificationBackgroundService(
            scopeFactory.Object,
            NullLogger<NotificationBackgroundService>.Instance);

        return (sut, scopeFactory);
    }

    // ── no-op when nothing is due ─────────────────────────────────────────────

    [Fact]
    public async Task ProcessRemindersAsync_NoReminders_UowNotCalledAndNoEmailSent()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var uow          = TestMocks.UnitOfWork();
        var emailService = TestMocks.EmailService();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([]);

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, unitOfWork: uow, emailService: emailService);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        emailService.Verify(e => e.SendSessionReminderAsync(It.IsAny<string>(), It.IsAny<SessionReminderData>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRemindersAsync_NoReminders_NotificationNotAdded()
    {
        var bookingRepo      = TestMocks.BookingRepo();
        var notificationRepo = TestMocks.NotificationRepo();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([]);

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, notificationRepo: notificationRepo);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never);
    }

    // ── notifications created ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessRemindersAsync_OneReminder_TwoNotificationsCreated()
    {
        var bookingRepo      = TestMocks.BookingRepo();
        var notificationRepo = TestMocks.NotificationRepo();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([Reminder()]);

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, notificationRepo: notificationRepo);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessRemindersAsync_TwoReminders_FourNotificationsCreated()
    {
        var bookingRepo      = TestMocks.BookingRepo();
        var notificationRepo = TestMocks.NotificationRepo();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([Reminder(), Reminder()]);

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, notificationRepo: notificationRepo);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Exactly(4));
    }

    [Fact]
    public async Task ProcessRemindersAsync_OneReminder_ClientNotificationLinkedToCorrectBookingAndUser()
    {
        var bookingRepo      = TestMocks.BookingRepo();
        var notificationRepo = TestMocks.NotificationRepo();
        var reminder         = Reminder();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([reminder]);

        var captured = new List<Notification>();
        notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
                        .Callback<Notification>(n => captured.Add(n));

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, notificationRepo: notificationRepo);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        var clientNotif = captured.Single(n => n.UserId == reminder.ClientId);
        clientNotif.BookingId.Should().Be(reminder.BookingId);
        clientNotif.Title.Should().Be("Session Reminder");
        clientNotif.Body.Should().Contain("Bob Jones");
    }

    [Fact]
    public async Task ProcessRemindersAsync_OneReminder_TrainerNotificationLinkedToCorrectBookingAndUser()
    {
        var bookingRepo      = TestMocks.BookingRepo();
        var notificationRepo = TestMocks.NotificationRepo();
        var reminder         = Reminder();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([reminder]);

        var captured = new List<Notification>();
        notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
                        .Callback<Notification>(n => captured.Add(n));

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, notificationRepo: notificationRepo);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        var trainerNotif = captured.Single(n => n.UserId == reminder.TrainerId);
        trainerNotif.BookingId.Should().Be(reminder.BookingId);
        trainerNotif.Title.Should().Be("Upcoming Session");
        trainerNotif.Body.Should().Contain("Alice Smith");
    }

    // ── unit of work ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessRemindersAsync_OneReminder_UowSavedOnce()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var uow         = TestMocks.UnitOfWork();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([Reminder()]);

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, unitOfWork: uow);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── email sending ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessRemindersAsync_OneReminder_EmailSentToBothClientAndTrainer()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([Reminder()]);

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        emailService.Verify(
            e => e.SendSessionReminderAsync("client@test.com", It.IsAny<SessionReminderData>()),
            Times.Once);
        emailService.Verify(
            e => e.SendSessionReminderAsync("trainer@test.com", It.IsAny<SessionReminderData>()),
            Times.Once);
        emailService.Verify(
            e => e.SendSessionReminderAsync(It.IsAny<string>(), It.IsAny<SessionReminderData>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessRemindersAsync_TwoReminders_FourEmailsSent()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        bookingRepo.Setup(r => r.GetDueForReminderAsync())
                   .ReturnsAsync([Reminder(), Reminder("c2@test.com", "t2@test.com")]);

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        emailService.Verify(
            e => e.SendSessionReminderAsync(It.IsAny<string>(), It.IsAny<SessionReminderData>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task ProcessRemindersAsync_OneReminder_ClientEmailDataIsCorrect()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        var reminder     = Reminder();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([reminder]);

        var captured = new List<(string Email, SessionReminderData Data)>();
        emailService.Setup(e => e.SendSessionReminderAsync(It.IsAny<string>(), It.IsAny<SessionReminderData>()))
                    .Callback<string, SessionReminderData>((email, data) => captured.Add((email, data)));

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        var (_, clientData) = captured.Single(x => x.Email == "client@test.com");
        clientData.IsTrainer.Should().BeFalse();
        clientData.RecipientFirstName.Should().Be("Alice");
        clientData.TrainerFullName.Should().Be("Bob Jones");
        clientData.StartTime.Should().Be(reminder.StartTime);
    }

    [Fact]
    public async Task ProcessRemindersAsync_OneReminder_TrainerEmailDataIsCorrect()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        var reminder     = Reminder();
        bookingRepo.Setup(r => r.GetDueForReminderAsync()).ReturnsAsync([reminder]);

        var captured = new List<(string Email, SessionReminderData Data)>();
        emailService.Setup(e => e.SendSessionReminderAsync(It.IsAny<string>(), It.IsAny<SessionReminderData>()))
                    .Callback<string, SessionReminderData>((email, data) => captured.Add((email, data)));

        var (sut, _) = CreateSut(bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessRemindersAsync(CancellationToken.None);

        var (_, trainerData) = captured.Single(x => x.Email == "trainer@test.com");
        trainerData.IsTrainer.Should().BeTrue();
        trainerData.RecipientFirstName.Should().Be("Bob");
        trainerData.ClientFullName.Should().Be("Alice Smith");
        trainerData.StartTime.Should().Be(reminder.StartTime);
    }
}
