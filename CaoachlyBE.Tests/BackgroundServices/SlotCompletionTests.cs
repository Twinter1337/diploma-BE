using CaoachlyBE.BackgroundServices;
using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CaoachlyBE.Tests.BackgroundServices;

public class SlotCompletionTests
{
    // ── test-data builders ────────────────────────────────────────────────────
    private static BookingCompletionModel BookedClient(
        string email       = "client@test.com",
        string firstName   = "Alice",
        string trainerName = "Bob Jones") => new()
    {
        BookingId       = Guid.NewGuid(),
        ClientId        = Guid.NewGuid(),
        ClientEmail     = email,
        ClientFirstName = firstName,
        TrainerFullName = trainerName,
        SlotStartTime   = new DateTime(2026, 5, 18, 10, 0, 0),
        SlotEndTime     = new DateTime(2026, 5, 18, 11, 0, 0),
    };

    // ── service factory ───────────────────────────────────────────────────────
    private static SlotCompletionBackgroundService CreateSut(
        Mock<IScheduleSlotRepository>? slotRepo    = null,
        Mock<IBookingRepository>?      bookingRepo = null,
        Mock<IEmailService>?           emailService = null,
        Mock<IUnitOfWork>?             unitOfWork  = null,
        Mock<IAchievementService>?     achievementService = null)
    {
        slotRepo     ??= TestMocks.ScheduleSlotRepo();
        bookingRepo  ??= TestMocks.BookingRepo();
        emailService ??= TestMocks.EmailService();
        unitOfWork   ??= TestMocks.UnitOfWork();
        achievementService ??= new Mock<IAchievementService>();
        achievementService
            .Setup(s => s.CheckAndAwardAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<int>());

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IScheduleSlotRepository))).Returns(slotRepo.Object);
        provider.Setup(p => p.GetService(typeof(IBookingRepository))).Returns(bookingRepo.Object);
        provider.Setup(p => p.GetService(typeof(IEmailService))).Returns(emailService.Object);
        provider.Setup(p => p.GetService(typeof(IAchievementService))).Returns(achievementService.Object);
        provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(unitOfWork.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new SlotCompletionBackgroundService(
            scopeFactory.Object,
            NullLogger<SlotCompletionBackgroundService>.Instance);
    }

    // ── no-op when nothing is expired ─────────────────────────────────────────

    [Fact]
    public async Task ProcessExpiredSlotsAsync_NoExpiredSlots_UowNotCalledAndNoEmailSent()
    {
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var uow          = TestMocks.UnitOfWork();
        var emailService = TestMocks.EmailService();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo, unitOfWork: uow, emailService: emailService);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        emailService.Verify(e => e.SendReviewRequestAsync(It.IsAny<string>(), It.IsAny<ReviewRequestData>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredSlotsAsync_NoExpiredSlots_SlotStatusNotUpdated()
    {
        var slotRepo = TestMocks.ScheduleSlotRepo();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        slotRepo.Verify(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<SlotStatus>()), Times.Never);
    }

    // ── slot status updated ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessExpiredSlotsAsync_OneExpiredSlot_SlotMarkedCompleted()
    {
        var slotId      = Guid.NewGuid();
        var slotRepo    = TestMocks.ScheduleSlotRepo();
        var bookingRepo = TestMocks.BookingRepo();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId)).ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        slotRepo.Verify(r => r.UpdateStatusAsync(slotId, SlotStatus.Completed), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredSlotsAsync_TwoExpiredSlots_BothMarkedCompleted()
    {
        var slotId1     = Guid.NewGuid();
        var slotId2     = Guid.NewGuid();
        var slotRepo    = TestMocks.ScheduleSlotRepo();
        var bookingRepo = TestMocks.BookingRepo();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId1, slotId2]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(It.IsAny<Guid>())).ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        slotRepo.Verify(r => r.UpdateStatusAsync(slotId1, SlotStatus.Completed), Times.Once);
        slotRepo.Verify(r => r.UpdateStatusAsync(slotId2, SlotStatus.Completed), Times.Once);
    }

    // ── booking status updated ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessExpiredSlotsAsync_SlotWithTwoBookings_BothBookingsMarkedCompleted()
    {
        var slotId      = Guid.NewGuid();
        var slotRepo    = TestMocks.ScheduleSlotRepo();
        var bookingRepo = TestMocks.BookingRepo();

        var clients = new[] { BookedClient(), BookedClient("c2@test.com", "Bob") };
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId)).ReturnsAsync(clients);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        bookingRepo.Verify(r => r.UpdateStatusAsync(clients[0].BookingId, BookingStatus.Completed), Times.Once);
        bookingRepo.Verify(r => r.UpdateStatusAsync(clients[1].BookingId, BookingStatus.Completed), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredSlotsAsync_SlotWithNoBookings_NoBookingStatusUpdate()
    {
        var slotId      = Guid.NewGuid();
        var slotRepo    = TestMocks.ScheduleSlotRepo();
        var bookingRepo = TestMocks.BookingRepo();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId)).ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        bookingRepo.Verify(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<BookingStatus>()), Times.Never);
    }

    // ── unit of work ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessExpiredSlotsAsync_OneExpiredSlot_UowSaved()
    {
        var slotId      = Guid.NewGuid();
        var slotRepo    = TestMocks.ScheduleSlotRepo();
        var bookingRepo = TestMocks.BookingRepo();
        var uow         = TestMocks.UnitOfWork();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId)).ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, unitOfWork: uow);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ── review request emails ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessExpiredSlotsAsync_SlotWithOneBooking_ReviewEmailSentToClient()
    {
        var slotId       = Guid.NewGuid();
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        var client       = BookedClient();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId)).ReturnsAsync([client]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        emailService.Verify(
            e => e.SendReviewRequestAsync("client@test.com", It.IsAny<ReviewRequestData>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredSlotsAsync_SlotWithNoBookings_NoEmailSent()
    {
        var slotId       = Guid.NewGuid();
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId)).ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        emailService.Verify(e => e.SendReviewRequestAsync(It.IsAny<string>(), It.IsAny<ReviewRequestData>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredSlotsAsync_SlotWithOneBooking_ReviewEmailDataCorrect()
    {
        var slotId       = Guid.NewGuid();
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        var client       = BookedClient();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId)).ReturnsAsync([client]);

        ReviewRequestData? captured = null;
        emailService.Setup(e => e.SendReviewRequestAsync(It.IsAny<string>(), It.IsAny<ReviewRequestData>()))
                    .Callback<string, ReviewRequestData>((_, d) => captured = d);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        captured!.ClientFirstName.Should().Be("Alice");
        captured.TrainerFullName.Should().Be("Bob Jones");
        captured.BookingId.Should().Be(client.BookingId);
        captured.SessionStartTime.Should().Be(client.SlotStartTime);
        captured.SessionEndTime.Should().Be(client.SlotEndTime);
    }

    [Fact]
    public async Task ProcessExpiredSlotsAsync_TwoSlotsWithOneBookingEach_TwoEmailsSent()
    {
        var slotId1      = Guid.NewGuid();
        var slotId2      = Guid.NewGuid();
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var emailService = TestMocks.EmailService();
        slotRepo.Setup(r => r.GetExpiredActiveAsync()).ReturnsAsync([slotId1, slotId2]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId1))
                   .ReturnsAsync([BookedClient("c1@test.com")]);
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(slotId2))
                   .ReturnsAsync([BookedClient("c2@test.com")]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.ProcessExpiredSlotsAsync(CancellationToken.None);

        emailService.Verify(
            e => e.SendReviewRequestAsync(It.IsAny<string>(), It.IsAny<ReviewRequestData>()),
            Times.Exactly(2));
    }
}
