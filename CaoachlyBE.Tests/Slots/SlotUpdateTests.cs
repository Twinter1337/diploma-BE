using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services;
using CaoachlyBE.Services.Interfaces;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Moq;

namespace CaoachlyBE.Tests.Slots;

public class SlotUpdateTests
{
    // ── shared identifiers ────────────────────────────────────────────────────
    private readonly Guid _trainerId = Guid.NewGuid();
    private readonly Guid _slotId    = Guid.NewGuid();
    private readonly DateTime _frozenNow = new(2026, 5, 17, 12, 0, 0);

    // ── test-data builders ────────────────────────────────────────────────────
    private ScheduleSlotModel ExistingSlot() => new()
    {
        Id          = _slotId,
        TrainerId   = _trainerId,
        Status      = SlotStatus.Available,
        Format      = SlotFormat.Online,
        StartTime   = _frozenNow.AddDays(2),
        EndTime     = _frozenNow.AddDays(2).AddHours(1),
        Price       = 500m,
        MaxClients  = 1,
        Description = "Original description",
        GymName     = null,
        GymAddress  = null,
        CreatedAt   = _frozenNow,
    };

    // DTO that changes nothing relative to ExistingSlot.
    private UpdateScheduleSlotDto NoChangeDto() => new();

    // DTO that changes StartTime — produces one detected change.
    private UpdateScheduleSlotDto StartTimeChangedDto() => new()
    {
        StartTime = _frozenNow.AddDays(3),
        EndTime   = _frozenNow.AddDays(3).AddHours(1),
    };

    private BookingCompletionModel BookedClient(string email, string firstName) => new()
    {
        BookingId      = Guid.NewGuid(),
        ClientId       = Guid.NewGuid(),
        ClientEmail    = email,
        ClientFirstName = firstName,
        TrainerFullName = "John Smith",
        SlotStartTime  = _frozenNow.AddDays(2),
        SlotEndTime    = _frozenNow.AddDays(2).AddHours(1),
    };

    // ── service factory ───────────────────────────────────────────────────────
    private TrainerService CreateSut(
        Mock<IScheduleSlotRepository>? slotRepo     = null,
        Mock<IBookingRepository>?      bookingRepo  = null,
        Mock<IUnitOfWork>?             unitOfWork   = null,
        Mock<IEmailService>?           emailService = null,
        Mock<ITimeProvider>?           timeProvider = null)
    {
        slotRepo     ??= TestMocks.ScheduleSlotRepo();
        bookingRepo  ??= TestMocks.BookingRepository();
        unitOfWork   ??= TestMocks.UnitOfWork();
        emailService ??= TestMocks.EmailService();
        timeProvider ??= TestMocks.TimeAt(_frozenNow);

        return new TrainerService(
            TestMocks.UserRepo().Object,
            TestMocks.TrainerInfoRepo().Object,
            slotRepo.Object,
            bookingRepo.Object,
            TestMocks.PaymentRepository().Object,
            TestMocks.TrainerDocumentRepo().Object,
            TestMocks.SupportTicketRepo().Object,
            TestMocks.BlobStorageService().Object,
            emailService.Object,
            unitOfWork.Object,
            timeProvider.Object);
    }

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSlotAsync_SlotNotFound_ThrowsKeyNotFoundException()
    {
        var slotRepo = TestMocks.ScheduleSlotRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync((ScheduleSlotModel?)null);

        var sut = CreateSut(slotRepo: slotRepo);

        await sut.Invoking(s => s.UpdateSlotAsync(_slotId, _trainerId, NoChangeDto()))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Slot not found*");
    }

    [Fact]
    public async Task UpdateSlotAsync_RequestingUserIsNotOwner_ThrowsUnauthorizedAccessException()
    {
        var slotRepo = TestMocks.ScheduleSlotRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(ExistingSlot());

        var sut = CreateSut(slotRepo: slotRepo);

        await sut.Invoking(s => s.UpdateSlotAsync(_slotId, Guid.NewGuid(), NoChangeDto()))
                 .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── update + persistence tests ────────────────────────────────────────────

    [Fact]
    public async Task UpdateSlotAsync_HappyPath_UpdateAsyncAndUowCalled()
    {
        var slotRepo  = TestMocks.ScheduleSlotRepo();
        var uow       = TestMocks.UnitOfWork();
        var dto       = StartTimeChangedDto();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(ExistingSlot());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(ExistingSlot()); // also for refresh
        // Second call (after update) returns the same slot — enough for DTO mapping.
        slotRepo.SetupSequence(r => r.GetByIdAsync(_slotId))
                .ReturnsAsync(ExistingSlot())   // first call: load before-state
                .ReturnsAsync(ExistingSlot());  // second call: reload after update

        var sut = CreateSut(slotRepo: slotRepo, unitOfWork: uow);

        await sut.UpdateSlotAsync(_slotId, _trainerId, dto);

        slotRepo.Verify(r => r.UpdateAsync(_slotId, dto), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSlotAsync_HappyPath_ResponseDtoBuiltFromRefreshedSlot()
    {
        var slotRepo = TestMocks.ScheduleSlotRepo();
        var updated  = ExistingSlot();
        updated.StartTime = _frozenNow.AddDays(3);
        updated.EndTime   = _frozenNow.AddDays(3).AddHours(1);

        slotRepo.SetupSequence(r => r.GetByIdAsync(_slotId))
                .ReturnsAsync(ExistingSlot()) // before-state
                .ReturnsAsync(updated);       // after-state (refreshed)

        var sut = CreateSut(slotRepo: slotRepo);

        var result = await sut.UpdateSlotAsync(_slotId, _trainerId, StartTimeChangedDto());

        result.Id.Should().Be(_slotId);
        result.StartTime.Should().Be(updated.StartTime);
        result.EndTime.Should().Be(updated.EndTime);
        result.TrainerId.Should().Be(_trainerId);
    }

    // ── notification tests ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSlotAsync_NoFieldsChanged_EmailNotSent()
    {
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepository();
        var emailService = TestMocks.EmailService();

        slotRepo.SetupSequence(r => r.GetByIdAsync(_slotId))
                .ReturnsAsync(ExistingSlot())
                .ReturnsAsync(ExistingSlot());

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.UpdateSlotAsync(_slotId, _trainerId, NoChangeDto());

        bookingRepo.Verify(
            r => r.GetConfirmedWithClientBySlotIdAsync(It.IsAny<Guid>()),
            Times.Never);
        emailService.Verify(
            e => e.SendSlotUpdateNotificationAsync(It.IsAny<string>(), It.IsAny<SlotUpdateNotificationData>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateSlotAsync_FieldsChangedNoBookedClients_EmailNotSent()
    {
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepository();
        var emailService = TestMocks.EmailService();

        slotRepo.SetupSequence(r => r.GetByIdAsync(_slotId))
                .ReturnsAsync(ExistingSlot())
                .ReturnsAsync(ExistingSlot());
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(_slotId))
                   .ReturnsAsync([]);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.UpdateSlotAsync(_slotId, _trainerId, StartTimeChangedDto());

        emailService.Verify(
            e => e.SendSlotUpdateNotificationAsync(It.IsAny<string>(), It.IsAny<SlotUpdateNotificationData>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateSlotAsync_FieldsChangedTwoBookedClients_EmailSentToEachClient()
    {
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepository();
        var emailService = TestMocks.EmailService();

        slotRepo.SetupSequence(r => r.GetByIdAsync(_slotId))
                .ReturnsAsync(ExistingSlot())
                .ReturnsAsync(ExistingSlot());

        var clients = new List<BookingCompletionModel>
        {
            BookedClient("client1@test.com", "Alice"),
            BookedClient("client2@test.com", "Bob"),
        };
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(_slotId))
                   .ReturnsAsync(clients);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.UpdateSlotAsync(_slotId, _trainerId, StartTimeChangedDto());

        emailService.Verify(
            e => e.SendSlotUpdateNotificationAsync("client1@test.com", It.IsAny<SlotUpdateNotificationData>()),
            Times.Once);
        emailService.Verify(
            e => e.SendSlotUpdateNotificationAsync("client2@test.com", It.IsAny<SlotUpdateNotificationData>()),
            Times.Once);
        emailService.Verify(
            e => e.SendSlotUpdateNotificationAsync(It.IsAny<string>(), It.IsAny<SlotUpdateNotificationData>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateSlotAsync_FieldsChanged_NotificationDataContainsDetectedChanges()
    {
        var slotRepo     = TestMocks.ScheduleSlotRepo();
        var bookingRepo  = TestMocks.BookingRepository();
        var emailService = TestMocks.EmailService();

        slotRepo.SetupSequence(r => r.GetByIdAsync(_slotId))
                .ReturnsAsync(ExistingSlot())
                .ReturnsAsync(ExistingSlot());
        bookingRepo.Setup(r => r.GetConfirmedWithClientBySlotIdAsync(_slotId))
                   .ReturnsAsync([BookedClient("client@test.com", "Alice")]);

        SlotUpdateNotificationData? capturedData = null;
        emailService.Setup(e => e.SendSlotUpdateNotificationAsync(It.IsAny<string>(), It.IsAny<SlotUpdateNotificationData>()))
                    .Callback<string, SlotUpdateNotificationData>((_, d) => capturedData = d);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, emailService: emailService);

        await sut.UpdateSlotAsync(_slotId, _trainerId, StartTimeChangedDto());

        capturedData!.ClientFirstName.Should().Be("Alice");
        capturedData.Changes.Should().NotBeEmpty();
        capturedData.Changes.Should().Contain(c => c.Field == "Start time");
    }
}
