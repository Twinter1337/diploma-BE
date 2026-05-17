using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services;
using CaoachlyBE.Services.Interfaces;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Moq;
using Stripe.Checkout;

namespace CaoachlyBE.Tests.Booking;

public class RetryPaymentTests
{
    // ── shared identifiers ────────────────────────────────────────────────────
    private readonly Guid _bookingId = Guid.NewGuid();
    private readonly Guid _clientId  = Guid.NewGuid();
    private readonly Guid _trainerId = Guid.NewGuid();
    private readonly Guid _slotId    = Guid.NewGuid();

    // Frozen "now" used by ITimeProvider.
    private readonly DateTime _frozenNow = new(2026, 5, 17, 12, 0, 0);

    // Default reservation window is 15 min (from TestMocks.DefaultConfiguration).
    // Booking created 20 min ago → past the window → late fee applies.
    private DateTime LateCreatedAt   => _frozenNow.AddMinutes(-20);
    // Booking created 5 min ago → within the window → no late fee.
    private DateTime OnTimeCreatedAt => _frozenNow.AddMinutes(-5);

    // ── test-data builders ────────────────────────────────────────────────────
    private BookingModel PendingBooking(DateTime? createdAt = null) => new()
    {
        Id        = _bookingId,
        SlotId    = _slotId,
        ClientId  = _clientId,
        Status    = BookingStatus.Pending,
        CreatedAt = createdAt ?? OnTimeCreatedAt,
        UpdatedAt = _frozenNow,
    };

    private PaymentModel Payment(decimal amount = 500m) => new()
    {
        Id            = Guid.NewGuid(),
        BookingId     = _bookingId,
        Amount        = amount,
        Currency      = "UAH",
        Status        = PaymentStatus.Pending,
        TransactionId = "cs_old_session",
        CreatedAt     = _frozenNow,
    };

    private ScheduleSlotModel Slot() => new()
    {
        Id        = _slotId,
        TrainerId = _trainerId,
        Status    = SlotStatus.Available,
        Format    = SlotFormat.Online,
        StartTime = _frozenNow.AddDays(2),
        EndTime   = _frozenNow.AddDays(2).AddHours(1),
        Price     = 500,
        MaxClients = 1,
    };

    private static Session OpenSession(string url = "https://pay.stripe.com/open") =>
        new() { Id = "cs_old_session", Url = url, Status = "open" };

    private static Session ExpiredSession() =>
        new() { Id = "cs_old_session", Url = "https://pay.stripe.com/expired", Status = "expired" };

    private static Session NewSession(string id = "cs_new_session", string url = "https://pay.stripe.com/new") =>
        new() { Id = id, Url = url };

    // ── service factory ───────────────────────────────────────────────────────
    private BookingService CreateSut(
        Mock<IBookingRepository>?      bookingRepo  = null,
        Mock<IPaymentRepository>?      paymentRepo  = null,
        Mock<IScheduleSlotRepository>? slotRepo     = null,
        Mock<IUserRepository>?         userRepo     = null,
        Mock<IUnitOfWork>?             unitOfWork   = null,
        Mock<IStripeCheckoutService>?  stripe       = null,
        Mock<ITimeProvider>?           timeProvider = null)
    {
        bookingRepo  ??= TestMocks.BookingRepo();
        paymentRepo  ??= TestMocks.PaymentRepo();
        slotRepo     ??= TestMocks.SlotRepo();
        userRepo     ??= TestMocks.UserRepo();
        unitOfWork   ??= TestMocks.UnitOfWork();
        timeProvider ??= TestMocks.TimeAt(_frozenNow);
        stripe       ??= TestMocks.StripeCheckout();

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

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryPaymentAsync_BookingNotFound_ThrowsKeyNotFoundException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync((BookingModel?)null);

        var sut = CreateSut(bookingRepo: bookingRepo);

        await sut.Invoking(s => s.RetryPaymentAsync(_bookingId, _clientId, "c@test.com"))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Booking not found*");
    }

    [Fact]
    public async Task RetryPaymentAsync_DifferentClient_ThrowsUnauthorizedAccessException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());

        var sut = CreateSut(bookingRepo: bookingRepo);

        await sut.Invoking(s => s.RetryPaymentAsync(_bookingId, Guid.NewGuid(), "c@test.com"))
                 .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RetryPaymentAsync_BookingNotPending_ThrowsInvalidOperationException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var booking = PendingBooking();
        booking.Status = BookingStatus.Confirmed;
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(booking);

        var sut = CreateSut(bookingRepo: bookingRepo);

        await sut.Invoking(s => s.RetryPaymentAsync(_bookingId, _clientId, "c@test.com"))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*pending bookings*");
    }

    [Fact]
    public async Task RetryPaymentAsync_PaymentNotFound_ThrowsKeyNotFoundException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync((PaymentModel?)null);

        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync(ExpiredSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo, stripe: stripe);

        await sut.Invoking(s => s.RetryPaymentAsync(_bookingId, _clientId, "c@test.com"))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Payment record not found*");
    }

    [Fact]
    public async Task RetryPaymentAsync_SlotNotFound_ThrowsKeyNotFoundException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(Payment());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync((ScheduleSlotModel?)null);

        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync(ExpiredSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, stripe: stripe);

        await sut.Invoking(s => s.RetryPaymentAsync(_bookingId, _clientId, "c@test.com"))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Slot not found*");
    }

    // ── existing open session tests ───────────────────────────────────────────

    [Fact]
    public async Task RetryPaymentAsync_ExistingSessionStillOpen_ReturnsExistingUrlWithoutNewSession()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(Payment(500m));

        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.GetAsync("cs_old_session")).ReturnsAsync(OpenSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo, stripe: stripe);

        var result = await sut.RetryPaymentAsync(_bookingId, _clientId, "c@test.com");

        result.CheckoutUrl.Should().Be("https://pay.stripe.com/open");
        result.ServiceFeeApplied.Should().BeFalse();
        result.TotalAmount.Should().Be(500m);
        result.Status.Should().Be(BookingStatus.Pending);

        // No new session should be created
        stripe.Verify(s => s.CreateAsync(It.IsAny<SessionCreateOptions>()), Times.Never);
    }

    // ── late-fee tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryPaymentAsync_WithinReservationWindow_NoLateFeeApplied()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();
        var uow         = TestMocks.UnitOfWork();

        // Created 5 min ago — within the 15-min window.
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking(OnTimeCreatedAt));
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(Payment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(Slot());

        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync(ExpiredSession());
        stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>())).ReturnsAsync(NewSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, unitOfWork: uow, stripe: stripe);

        var result = await sut.RetryPaymentAsync(_bookingId, _clientId, "c@test.com");

        result.ServiceFeeApplied.Should().BeFalse();
        result.TotalAmount.Should().Be(500m);

        // Amount record must NOT be updated when there's no late fee.
        paymentRepo.Verify(r => r.UpdateAmountAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task RetryPaymentAsync_PastReservationWindow_LateFeeApplied()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();
        var uow         = TestMocks.UnitOfWork();

        // Created 20 min ago — past the 15-min window.
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking(LateCreatedAt));
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(Payment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(Slot());

        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync(ExpiredSession());
        stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>())).ReturnsAsync(NewSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, unitOfWork: uow, stripe: stripe);

        var result = await sut.RetryPaymentAsync(_bookingId, _clientId, "c@test.com");

        // 500 * 1.20 = 600
        result.ServiceFeeApplied.Should().BeTrue();
        result.TotalAmount.Should().Be(600m);

        paymentRepo.Verify(r => r.UpdateAmountAsync(_bookingId, 600m), Times.Once);
    }

    [Fact]
    public async Task RetryPaymentAsync_PastReservationWindow_StripeSessionCreatedWithLateFeeAmount()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();

        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking(LateCreatedAt));
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(Payment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(Slot());

        SessionCreateOptions? capturedOptions = null;
        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync(ExpiredSession());
        stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>()))
              .Callback<SessionCreateOptions>(o => capturedOptions = o)
              .ReturnsAsync(NewSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, stripe: stripe);

        await sut.RetryPaymentAsync(_bookingId, _clientId, "c@test.com");

        capturedOptions!.LineItems.Single().PriceData.UnitAmount.Should().Be(60000); // 600 UAH × 100
        capturedOptions.LineItems.Single().PriceData.ProductData.Name.Should().Contain("20% late payment fee");
    }

    // ── session update tests ──────────────────────────────────────────────────

    [Fact]
    public async Task RetryPaymentAsync_ExpiredSession_NewSessionIdSavedAndUowCalled()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();
        var uow         = TestMocks.UnitOfWork();

        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking(OnTimeCreatedAt));
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(Payment());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(Slot());

        var stripe = TestMocks.StripeCheckout();
        stripe.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync(ExpiredSession());
        stripe.Setup(s => s.CreateAsync(It.IsAny<SessionCreateOptions>()))
              .ReturnsAsync(NewSession("cs_new_session"));

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, unitOfWork: uow, stripe: stripe);

        var result = await sut.RetryPaymentAsync(_bookingId, _clientId, "c@test.com");

        paymentRepo.Verify(r => r.UpdateStripeSessionIdAsync(_bookingId, "cs_new_session"), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.CheckoutUrl.Should().Be("https://pay.stripe.com/new");
    }
}
