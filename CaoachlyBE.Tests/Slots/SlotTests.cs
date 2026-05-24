using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Moq;

namespace CaoachlyBE.Tests.Slots;

public class SlotTests
{
    // ── shared identifiers ────────────────────────────────────────────────────
    private readonly Guid _trainerId = Guid.NewGuid();
    private readonly DateTime _frozenNow = new(2026, 5, 17, 12, 0, 0);

    // ── test-data builders ────────────────────────────────────────────────────
    private UserModel TrainerUser() => new()
    {
        Id           = _trainerId,
        FirstName    = "John",
        LastName     = "Smith",
        Email        = "trainer@test.com",
        PasswordHash = "x",
        Role         = UserRole.Trainer,
        IsActive     = true,
        CreatedAt    = _frozenNow,
        UpdatedAt    = _frozenNow,
    };

    // Valid slot: 2 h duration, starts tomorrow.
    private CreateScheduleSlotDto ValidDto() => new()
    {
        StartTime      = _frozenNow.AddDays(1),
        EndTime        = _frozenNow.AddDays(1).AddHours(2),
        Format         = SlotFormat.Online,
        PricePerSession = 500m,
        MaxClients     = 1,
        Description    = "Morning session",
        GymName        = null,
        GymAddress     = null,
    };

    // ── service factory ───────────────────────────────────────────────────────
    private TrainerService CreateSut(
        Mock<IUserRepository>?         userRepo     = null,
        Mock<IScheduleSlotRepository>? slotRepo     = null,
        Mock<IUnitOfWork>?             unitOfWork   = null,
        Mock<ITimeProvider>?           timeProvider = null)
    {
        userRepo     ??= TestMocks.UserRepo();
        slotRepo     ??= TestMocks.ScheduleSlotRepo();
        unitOfWork   ??= TestMocks.UnitOfWork();
        timeProvider ??= TestMocks.TimeAt(_frozenNow);

        return new TrainerService(
            userRepo.Object,
            TestMocks.TrainerInfoRepo().Object,
            slotRepo.Object,
            TestMocks.BookingRepository().Object,
            TestMocks.PaymentRepository().Object,
            TestMocks.TrainerDocumentRepo().Object,
            TestMocks.SupportTicketRepo().Object,
            TestMocks.BlobStorageService().Object,
            TestMocks.EmailService().Object,
            unitOfWork.Object,
            timeProvider.Object);
    }

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSlotAsync_TrainerNotFound_ThrowsKeyNotFoundException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync((UserModel?)null);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.CreateSlotAsync(_trainerId, _trainerId, ValidDto()))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Trainer not found*");
    }

    [Fact]
    public async Task CreateSlotAsync_UserIsNotTrainer_ThrowsKeyNotFoundException()
    {
        var userRepo = TestMocks.UserRepo();
        var client = TrainerUser();
        client.Role = UserRole.Client;
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(client);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.CreateSlotAsync(_trainerId, _trainerId, ValidDto()))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Trainer not found*");
    }

    [Fact]
    public async Task CreateSlotAsync_RequestingUserIsNotOwner_ThrowsUnauthorizedAccessException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var sut = CreateSut(userRepo: userRepo);

        var differentUserId = Guid.NewGuid();
        await sut.Invoking(s => s.CreateSlotAsync(_trainerId, differentUserId, ValidDto()))
                 .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateSlotAsync_DurationLessThan60Minutes_ThrowsArgumentException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var dto = ValidDto();
        dto.EndTime = dto.StartTime.AddMinutes(45);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.CreateSlotAsync(_trainerId, _trainerId, dto))
                 .Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*at least 60 minutes*");
    }

    [Fact]
    public async Task CreateSlotAsync_DurationMoreThan6Hours_ThrowsArgumentException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var dto = ValidDto();
        dto.EndTime = dto.StartTime.AddHours(7);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.CreateSlotAsync(_trainerId, _trainerId, dto))
                 .Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*not exceed 6 hours*");
    }

    [Fact]
    public async Task CreateSlotAsync_StartTimeInThePast_ThrowsArgumentException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var dto = ValidDto();
        dto.StartTime = _frozenNow.AddHours(-1);
        dto.EndTime   = _frozenNow.AddHours(1);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.CreateSlotAsync(_trainerId, _trainerId, dto))
                 .Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*must be in the future*");
    }

    [Fact]
    public async Task CreateSlotAsync_OverlappingSlotExists_ThrowsInvalidOperationException()
    {
        var userRepo = TestMocks.UserRepo();
        var slotRepo = TestMocks.ScheduleSlotRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());
        slotRepo.Setup(r => r.HasConflictAsync(_trainerId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(true);

        var sut = CreateSut(userRepo: userRepo, slotRepo: slotRepo);

        await sut.Invoking(s => s.CreateSlotAsync(_trainerId, _trainerId, ValidDto()))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*slot already exists*");
    }

    // ── happy-path tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSlotAsync_HappyPath_SlotSavedWithCorrectFieldsAndUowCalled()
    {
        var userRepo   = TestMocks.UserRepo();
        var slotRepo   = TestMocks.ScheduleSlotRepo();
        var uow        = TestMocks.UnitOfWork();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        ScheduleSlotModel? capturedSlot = null;
        slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlotModel>()))
                .Callback<ScheduleSlotModel>(s => capturedSlot = s);

        var sut = CreateSut(userRepo: userRepo, slotRepo: slotRepo, unitOfWork: uow);
        var dto = ValidDto();

        await sut.CreateSlotAsync(_trainerId, _trainerId, dto);

        slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlotModel>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedSlot!.TrainerId.Should().Be(_trainerId);
        capturedSlot.StartTime.Should().Be(dto.StartTime.DateTime);
        capturedSlot.EndTime.Should().Be(dto.EndTime.DateTime);
        capturedSlot.Format.Should().Be(dto.Format);
        capturedSlot.Price.Should().Be(dto.PricePerSession);
        capturedSlot.MaxClients.Should().Be(dto.MaxClients);
        capturedSlot.Status.Should().Be(SlotStatus.Available);
        capturedSlot.Description.Should().Be(dto.Description);
    }

    [Fact]
    public async Task CreateSlotAsync_HappyPath_ResponseDtoHasCorrectValues()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var sut = CreateSut(userRepo: userRepo);
        var dto = ValidDto();

        var result = await sut.CreateSlotAsync(_trainerId, _trainerId, dto);

        result.Id.Should().NotBeEmpty();
        result.TrainerId.Should().Be(_trainerId);
        result.StartTime.Should().Be(dto.StartTime.DateTime);
        result.EndTime.Should().Be(dto.EndTime.DateTime);
        result.Format.Should().Be(dto.Format);
        result.Price.Should().Be(dto.PricePerSession);
        result.MaxClients.Should().Be(dto.MaxClients);
        result.Status.Should().Be(SlotStatus.Available);
        result.Description.Should().Be(dto.Description);
    }

    [Fact]
    public async Task CreateSlotAsync_HappyPath_SlotCreatedAtMatchesFrozenClock()
    {
        var userRepo = TestMocks.UserRepo();
        var slotRepo = TestMocks.ScheduleSlotRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        ScheduleSlotModel? capturedSlot = null;
        slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlotModel>()))
                .Callback<ScheduleSlotModel>(s => capturedSlot = s);

        var sut = CreateSut(userRepo: userRepo, slotRepo: slotRepo);

        await sut.CreateSlotAsync(_trainerId, _trainerId, ValidDto());

        capturedSlot!.CreatedAt.Should().Be(_frozenNow);
    }
}
