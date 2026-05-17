using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Bookings;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services;
using CaoachlyBE.Services.Interfaces;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Moq;
using Stripe.Checkout;

namespace CaoachlyBE.Tests.Booking;

public class BookingServiceTests
{
    // ── shared identifiers ────────────────────────────────────────────────────
    private readonly Guid _clientId  = Guid.NewGuid();
    private readonly Guid _trainerId = Guid.NewGuid();
    private readonly Guid _slotId    = Guid.NewGuid();
    private readonly DateTime _frozenNow = new(2026, 5, 17, 10, 0, 0);

    // ── test-data builders ────────────────────────────────────────────────────
    private ScheduleSlotModel AvailableSlot() => new()
    {
        Id         = _slotId,
        TrainerId  = _trainerId,
        Status     = SlotStatus.Available,
        StartTime  = _frozenNow.AddDays(2),
        EndTime    = _frozenNow.AddDays(2).AddHours(1),
        Price      = 500,
        MaxClients = 1,
    };

    private ScheduleSlotModel SlotWithStatus(SlotStatus status)
    {
        var s = AvailableSlot();
        s.Status = status;
        return s;
    }

    private CreateBookingDto ValidDto() => new()
    {
        SlotId          = _slotId,
        TotalAmount     = 500m,
        ReminderMinutes = 30,
    };

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

    private static Session FakeSession(string id = "sess_test", string url = "https://pay.stripe.com/sess_test") =>
        new() { Id = id, Url = url };

    // ── service factory ───────────────────────────────────────────────────────
    private BookingService CreateSut(
        Mock<IScheduleSlotRepository>? slotRepo     = null,
        Mock<IBookingRepository>?      bookingRepo  = null,
        Mock<IPaymentRepository>?      paymentRepo  = null,
        Mock<IUserRepository>?         userRepo     = null,
        Mock<IUnitOfWork>?             unitOfWork   = null,
        Mock<IStripeCheckoutService>?  stripe       = null,
        Mock<ITimeProvider>?           timeProvider = null)
    {
        slotRepo     ??= TestMocks.SlotRepo();
        bookingRepo  ??= TestMocks.BookingRepo();
        paymentRepo  ??= TestMocks.PaymentRepo();
        userRepo     ??= TestMocks.UserRepo();
        unitOfWork   ??= TestMocks.UnitOfWork();
        timeProvider ??= TestMocks.TimeAt(_frozenNow);

        // If no stripe mock provided, create one with a default session response.
        // When the caller provides their own mock they own its setup entirely.
        if (stripe is null)
        {
            stripe = TestMocks.StripeCheckout();
            stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>()))
                  .ReturnsAsync(FakeSession());
        }

        return new BookingService(
            bookingRepo.Object,
            paymentRepo.Object,
            slotRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            TestMocks.EmailService().Object,
            TestMocks.DefaultConfiguration(),
            new Mock<IMapper>().Object,
            stripe.Object,
            TestMocks.StripeRefund().Object,
            timeProvider.Object);
    }

    // ── guard / validation tests ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SlotNotFound_ThrowsKeyNotFoundException()
    {
        var slotRepo = TestMocks.SlotRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync((ScheduleSlotModel?)null);

        var sut = CreateSut(slotRepo: slotRepo);

        await sut.Invoking(s => s.CreateAsync(_clientId, "c@test.com", "Client", ValidDto()))
                 .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_SlotIsBooked_ThrowsInvalidOperationException()
    {
        var slotRepo = TestMocks.SlotRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(SlotWithStatus(SlotStatus.Booked));

        var sut = CreateSut(slotRepo: slotRepo);

        await sut.Invoking(s => s.CreateAsync(_clientId, "c@test.com", "Client", ValidDto()))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*no longer available*");
    }

    [Fact]
    public async Task CreateAsync_SlotIsCompleted_ThrowsInvalidOperationException()
    {
        var slotRepo = TestMocks.SlotRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(SlotWithStatus(SlotStatus.Completed));

        var sut = CreateSut(slotRepo: slotRepo);

        await sut.Invoking(s => s.CreateAsync(_clientId, "c@test.com", "Client", ValidDto()))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*no longer available*");
    }

    [Fact]
    public async Task CreateAsync_ClientHasConflictingBooking_ThrowsInvalidOperationException()
    {
        var slotRepo    = TestMocks.SlotRepo();
        var bookingRepo = TestMocks.BookingRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(AvailableSlot());
        bookingRepo.Setup(r => r.HasConflictAsync(_clientId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                   .ReturnsAsync(true);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo);

        await sut.Invoking(s => s.CreateAsync(_clientId, "c@test.com", "Client", ValidDto()))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*overlaps*");
    }

    // ── happy-path tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_HappyPath_BookingAndPaymentSavedAndUowCalled()
    {
        var slotRepo    = TestMocks.SlotRepo();
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var uow         = TestMocks.UnitOfWork();

        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(AvailableSlot());

        BookingModel? capturedBooking = null;
        PaymentModel? capturedPayment = null;
        bookingRepo.Setup(r => r.AddAsync(It.IsAny<BookingModel>()))
                   .Callback<BookingModel>(b => capturedBooking = b);
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<PaymentModel>()))
                   .Callback<PaymentModel>(p => capturedPayment = p);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo,
                            paymentRepo: paymentRepo, unitOfWork: uow);

        await sut.CreateAsync(_clientId, "c@test.com", "Client", ValidDto());

        bookingRepo.Verify(r => r.AddAsync(It.IsAny<BookingModel>()), Times.Once);
        paymentRepo.Verify(r => r.AddAsync(It.IsAny<PaymentModel>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedBooking!.ClientId.Should().Be(_clientId);
        capturedBooking.SlotId.Should().Be(_slotId);
        capturedBooking.Status.Should().Be(BookingStatus.Pending);
        capturedBooking.ReminderMinutes.Should().Be(30);

        capturedPayment!.Amount.Should().Be(500m);
        capturedPayment.Currency.Should().Be("UAH");
        capturedPayment.Status.Should().Be(PaymentStatus.Pending);
        capturedPayment.PaymentMethod.Should().Be(PaymentMethod.Online);
        capturedPayment.TransactionId.Should().Be("sess_test");
    }

    [Fact]
    public async Task CreateAsync_HappyPath_ResponseDtoHasCorrectValues()
    {
        var slotRepo = TestMocks.SlotRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(AvailableSlot());

        var sut = CreateSut(slotRepo: slotRepo);

        var result = await sut.CreateAsync(_clientId, "c@test.com", "Client", ValidDto());

        result.BookingId.Should().NotBeEmpty();
        result.CheckoutUrl.Should().Be("https://pay.stripe.com/sess_test");
        result.Status.Should().Be(BookingStatus.Pending);
        result.ServiceFeeApplied.Should().BeFalse();
        result.TotalAmount.Should().Be(500m);
    }

    [Fact]
    public async Task CreateAsync_HappyPath_StripeSessionCreatedWithCorrectAmountAndMetadata()
    {
        var slotRepo = TestMocks.SlotRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(AvailableSlot());

        SessionCreateOptions? capturedOptions = null;
        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>()))
              .Callback<SessionCreateOptions>(o => capturedOptions = o)
              .ReturnsAsync(FakeSession());

        var sut = CreateSut(slotRepo: slotRepo, stripe: stripe);

        var result = await sut.CreateAsync(_clientId, "c@test.com", "Client", ValidDto());

        capturedOptions.Should().NotBeNull();
        capturedOptions!.CustomerEmail.Should().Be("c@test.com");
        capturedOptions.Mode.Should().Be("payment");

        var lineItem = capturedOptions.LineItems.Single();
        lineItem.PriceData.UnitAmount.Should().Be(50000); // 500 UAH × 100
        lineItem.PriceData.Currency.Should().Be("uah");

        capturedOptions.Metadata["bookingId"].Should().Be(result.BookingId.ToString());
    }

    [Fact]
    public async Task CreateAsync_HappyPath_StripeSessionProductNameContainsTrainerFullName()
    {
        var slotRepo = TestMocks.SlotRepo();
        var userRepo = TestMocks.UserRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(AvailableSlot());
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        SessionCreateOptions? capturedOptions = null;
        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>()))
              .Callback<SessionCreateOptions>(o => capturedOptions = o)
              .ReturnsAsync(FakeSession());

        var sut = CreateSut(slotRepo: slotRepo, userRepo: userRepo, stripe: stripe);

        await sut.CreateAsync(_clientId, "c@test.com", "Client", ValidDto());

        capturedOptions!.LineItems.Single().PriceData.ProductData.Name
            .Should().Contain("John Smith");
    }

    [Fact]
    public async Task CreateAsync_TrainerNotFound_FallsBackToTrainerNameWithoutException()
    {
        var slotRepo = TestMocks.SlotRepo();
        var userRepo = TestMocks.UserRepo();
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(AvailableSlot());
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync((UserModel?)null);

        SessionCreateOptions? capturedOptions = null;
        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>()))
              .Callback<SessionCreateOptions>(o => capturedOptions = o)
              .ReturnsAsync(FakeSession());

        var sut = CreateSut(slotRepo: slotRepo, userRepo: userRepo, stripe: stripe);

        await sut.Invoking(s => s.CreateAsync(_clientId, "c@test.com", "Client", ValidDto()))
                 .Should().NotThrowAsync();

        capturedOptions!.LineItems.Single().PriceData.ProductData.Name
            .Should().Contain("Trainer");
    }

    [Fact]
    public async Task CreateAsync_HappyPath_BookingTimestampsMatchFrozenClock()
    {
        var slotRepo     = TestMocks.SlotRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var timeProvider = TestMocks.TimeAt(_frozenNow);
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(AvailableSlot());

        BookingModel? capturedBooking = null;
        bookingRepo.Setup(r => r.AddAsync(It.IsAny<BookingModel>()))
                   .Callback<BookingModel>(b => capturedBooking = b);

        var sut = CreateSut(slotRepo: slotRepo, bookingRepo: bookingRepo, timeProvider: timeProvider);

        await sut.CreateAsync(_clientId, "c@test.com", "Client", ValidDto());

        capturedBooking!.CreatedAt.Should().Be(_frozenNow);
        capturedBooking.UpdatedAt.Should().Be(_frozenNow);
    }
}
