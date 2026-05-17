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
using Stripe;

namespace CaoachlyBE.Tests.Booking;

public class BookingCancellationTests
{
    // ── shared identifiers ────────────────────────────────────────────────────
    private readonly Guid _bookingId = Guid.NewGuid();
    private readonly Guid _clientId  = Guid.NewGuid();
    private readonly Guid _trainerId = Guid.NewGuid();
    private readonly Guid _slotId    = Guid.NewGuid();
    private readonly DateTime _frozenNow = new(2026, 5, 17, 12, 0, 0);

    // ── test-data builders ────────────────────────────────────────────────────
    private BookingModel PendingBooking() => new()
    {
        Id        = _bookingId,
        SlotId    = _slotId,
        ClientId  = _clientId,
        Status    = BookingStatus.Pending,
        CreatedAt = _frozenNow,
        UpdatedAt = _frozenNow,
    };

    // Slot starts more than 24 h from frozen now → 100% refund.
    private ScheduleSlotModel FarSlot() => new()
    {
        Id        = _slotId,
        TrainerId = _trainerId,
        Status    = SlotStatus.Booked,
        Format    = SlotFormat.Online,
        StartTime = _frozenNow.AddHours(25),
        EndTime   = _frozenNow.AddHours(26),
        Price     = 500,
        MaxClients = 1,
    };

    // Slot starts within 24 h → 50% refund.
    private ScheduleSlotModel NearSlot()
    {
        var s = FarSlot();
        s.StartTime = _frozenNow.AddHours(12);
        s.EndTime   = _frozenNow.AddHours(13);
        return s;
    }

    private PaymentModel PaidPayment(decimal amount = 500m) => new()
    {
        Id            = Guid.NewGuid(),
        BookingId     = _bookingId,
        Amount        = amount,
        Currency      = "UAH",
        Status        = PaymentStatus.Paid,
        TransactionId = "pi_test",
        CreatedAt     = _frozenNow,
    };

    private PaymentModel PendingPayment()
    {
        var p = PaidPayment();
        p.Status = PaymentStatus.Pending;
        return p;
    }

    private UserModel ClientUser() => new()
    {
        Id           = _clientId,
        FirstName    = "Anna",
        LastName     = "Koval",
        Email        = "anna@test.com",
        PasswordHash = "x",
        Role         = UserRole.Client,
        IsActive     = true,
        CreatedAt    = _frozenNow,
        UpdatedAt    = _frozenNow,
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

    // ── service factory ───────────────────────────────────────────────────────
    private BookingService CreateSut(
        Mock<IBookingRepository>?      bookingRepo   = null,
        Mock<IPaymentRepository>?      paymentRepo   = null,
        Mock<IScheduleSlotRepository>? slotRepo      = null,
        Mock<IUserRepository>?         userRepo      = null,
        Mock<IUnitOfWork>?             unitOfWork    = null,
        Mock<IStripeRefundService>?    stripeRefund  = null,
        Mock<IEmailService>?           emailService  = null,
        Mock<ITimeProvider>?           timeProvider  = null)
    {
        bookingRepo  ??= TestMocks.BookingRepo();
        paymentRepo  ??= TestMocks.PaymentRepo();
        slotRepo     ??= TestMocks.SlotRepo();
        userRepo     ??= TestMocks.UserRepo();
        unitOfWork   ??= TestMocks.UnitOfWork();
        stripeRefund ??= TestMocks.StripeRefund();
        emailService ??= TestMocks.EmailService();
        timeProvider ??= TestMocks.TimeAt(_frozenNow);

        return new BookingService(
            bookingRepo.Object,
            paymentRepo.Object,
            slotRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            emailService.Object,
            TestMocks.DefaultConfiguration(),
            new Mock<IMapper>().Object,
            TestMocks.StripeCheckout().Object,
            stripeRefund.Object,
            timeProvider.Object);
    }

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_BookingNotFound_ThrowsKeyNotFoundException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync((BookingModel?)null);

        var sut = CreateSut(bookingRepo: bookingRepo);

        await sut.Invoking(s => s.CancelAsync(_bookingId, _clientId))
                 .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CancelAsync_DifferentClient_ThrowsUnauthorizedAccessException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());

        var sut = CreateSut(bookingRepo: bookingRepo);

        var differentClientId = Guid.NewGuid();
        await sut.Invoking(s => s.CancelAsync(_bookingId, differentClientId))
                 .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CancelAsync_BookingAlreadyCancelled_ThrowsInvalidOperationException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var booking = PendingBooking();
        booking.Status = BookingStatus.Cancelled;
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(booking);

        var sut = CreateSut(bookingRepo: bookingRepo);

        await sut.Invoking(s => s.CancelAsync(_bookingId, _clientId))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*cancelled or completed*");
    }

    [Fact]
    public async Task CancelAsync_BookingAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var booking = PendingBooking();
        booking.Status = BookingStatus.Completed;
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(booking);

        var sut = CreateSut(bookingRepo: bookingRepo);

        await sut.Invoking(s => s.CancelAsync(_bookingId, _clientId))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*cancelled or completed*");
    }

    // ── refund-percentage tests ───────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_SessionMoreThan24hAway_Refund100Percent()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var paymentRepo  = TestMocks.PaymentRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var stripeRefund = TestMocks.StripeRefund();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PaidPayment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(FarSlot());

        RefundCreateOptions? capturedRefund = null;
        stripeRefund.Setup(s => s.CreateAsync(It.IsAny<RefundCreateOptions>()))
                    .Callback<RefundCreateOptions>(o => capturedRefund = o);

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, stripeRefund: stripeRefund);

        var result = await sut.CancelAsync(_bookingId, _clientId);

        result.RefundPercentage.Should().Be(100);
        result.RefundAmount.Should().Be(500m);
        capturedRefund!.Amount.Should().Be(50000); // 500 UAH × 100
    }

    [Fact]
    public async Task CancelAsync_SessionLessThan24hAway_Refund50Percent()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var paymentRepo  = TestMocks.PaymentRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var stripeRefund = TestMocks.StripeRefund();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PaidPayment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(NearSlot());

        RefundCreateOptions? capturedRefund = null;
        stripeRefund.Setup(s => s.CreateAsync(It.IsAny<RefundCreateOptions>()))
                    .Callback<RefundCreateOptions>(o => capturedRefund = o);

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, stripeRefund: stripeRefund);

        var result = await sut.CancelAsync(_bookingId, _clientId);

        result.RefundPercentage.Should().Be(50);
        result.RefundAmount.Should().Be(250m);
        capturedRefund!.Amount.Should().Be(25000); // 250 UAH × 100
    }

    [Fact]
    public async Task CancelAsync_SlotNotFound_DefaultsTo100PercentRefund()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PaidPayment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync((ScheduleSlotModel?)null);

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo, slotRepo: slotRepo);

        var result = await sut.CancelAsync(_bookingId, _clientId);

        result.RefundPercentage.Should().Be(100);
        result.RefundAmount.Should().Be(500m);
    }

    // ── payment-state tests ───────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_PaymentStillPending_NoStripeRefundAndZeroRefundAmount()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var paymentRepo  = TestMocks.PaymentRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var stripeRefund = TestMocks.StripeRefund();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PendingPayment());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(FarSlot());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, stripeRefund: stripeRefund);

        var result = await sut.CancelAsync(_bookingId, _clientId);

        stripeRefund.Verify(s => s.CreateAsync(It.IsAny<RefundCreateOptions>()), Times.Never);
        result.RefundAmount.Should().Be(0m);
    }

    [Fact]
    public async Task CancelAsync_HappyPath_BookingCancelledByClientAndUowSaved()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();
        var uow         = TestMocks.UnitOfWork();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PendingPayment());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(FarSlot());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, unitOfWork: uow);

        await sut.CancelAsync(_bookingId, _clientId);

        bookingRepo.Verify(
            r => r.CancelAsync(_bookingId, CancelledBy.Client, null),
            Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_HappyPath_ResponseDtoHasCorrectValues()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var slotRepo    = TestMocks.SlotRepo();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PaidPayment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(FarSlot());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo, slotRepo: slotRepo);

        var result = await sut.CancelAsync(_bookingId, _clientId);

        result.BookingId.Should().Be(_bookingId);
        result.Status.Should().Be((short)BookingStatus.Cancelled);
        result.RefundAmount.Should().Be(500m);
        result.RefundPercentage.Should().Be(100);
    }

    // ── refund notification email tests ───────────────────────────────────────

    [Fact]
    public async Task CancelAsync_PaidWithRefund_RefundEmailSentWithCorrectData()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var paymentRepo  = TestMocks.PaymentRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var userRepo     = TestMocks.UserRepo();
        var emailService = TestMocks.EmailService();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PaidPayment(500m));
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(FarSlot());
        userRepo.Setup(r => r.GetByIdAsync(_clientId)).ReturnsAsync(ClientUser());
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        RefundNotificationData? capturedData = null;
        emailService.Setup(e => e.SendRefundNotificationAsync(It.IsAny<string>(), It.IsAny<RefundNotificationData>()))
                    .Callback<string, RefundNotificationData>((_, d) => capturedData = d);

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, userRepo: userRepo, emailService: emailService);

        await sut.CancelAsync(_bookingId, _clientId);

        emailService.Verify(
            e => e.SendRefundNotificationAsync("anna@test.com", It.IsAny<RefundNotificationData>()),
            Times.Once);

        capturedData!.ClientFirstName.Should().Be("Anna");
        capturedData.TrainerName.Should().Be("John Smith");
        capturedData.RefundAmount.Should().Be(500m);
        capturedData.Currency.Should().Be("UAH");
        capturedData.RefundPercentage.Should().Be(100);
        capturedData.CancelledAt.Should().Be(_frozenNow);
    }

    [Fact]
    public async Task CancelAsync_ClientHasNoEmail_RefundEmailNotSent()
    {
        var bookingRepo  = TestMocks.BookingRepo();
        var paymentRepo  = TestMocks.PaymentRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var userRepo     = TestMocks.UserRepo();
        var emailService = TestMocks.EmailService();
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(PendingBooking());
        paymentRepo.Setup(r => r.GetByBookingIdAsync(_bookingId)).ReturnsAsync(PaidPayment());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(FarSlot());

        var clientWithNoEmail = ClientUser();
        clientWithNoEmail.Email = null!;
        userRepo.Setup(r => r.GetByIdAsync(_clientId)).ReturnsAsync(clientWithNoEmail);

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo,
                            slotRepo: slotRepo, userRepo: userRepo, emailService: emailService);

        await sut.CancelAsync(_bookingId, _clientId);

        emailService.Verify(
            e => e.SendRefundNotificationAsync(It.IsAny<string>(), It.IsAny<RefundNotificationData>()),
            Times.Never);
    }
}
