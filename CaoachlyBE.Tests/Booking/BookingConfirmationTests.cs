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

namespace CaoachlyBE.Tests.Booking;

public class BookingConfirmationTests
{
    // ── shared identifiers ────────────────────────────────────────────────────
    private readonly Guid _bookingId  = Guid.NewGuid();
    private readonly Guid _clientId   = Guid.NewGuid();
    private readonly Guid _trainerId  = Guid.NewGuid();
    private readonly Guid _slotId     = Guid.NewGuid();
    private readonly DateTime _frozenNow   = new(2026, 5, 17, 12, 0, 0);
    private const string StripeSessionId  = "cs_test_session";
    private const string PaymentIntentId  = "pi_test_intent";

    // ── test-data builders ────────────────────────────────────────────────────
    private PaymentModel PaidPayment() => new()
    {
        Id            = Guid.NewGuid(),
        BookingId     = _bookingId,
        Amount        = 500m,
        Currency      = "UAH",
        Status        = PaymentStatus.Paid,
        TransactionId = StripeSessionId,
        CreatedAt     = _frozenNow,
    };

    private BookingModel ConfirmedBooking() => new()
    {
        Id        = _bookingId,
        SlotId    = _slotId,
        ClientId  = _clientId,
        Status    = BookingStatus.Pending,
        CreatedAt = _frozenNow,
        UpdatedAt = _frozenNow,
    };

    private ScheduleSlotModel OnlineSlot() => new()
    {
        Id        = _slotId,
        TrainerId = _trainerId,
        Status    = SlotStatus.Booked,
        Format    = SlotFormat.Online,
        StartTime = _frozenNow.AddDays(1),
        EndTime   = _frozenNow.AddDays(1).AddHours(1),
        Price     = 500,
        MaxClients = 1,
    };

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
        Mock<IPaymentRepository>?     paymentRepo  = null,
        Mock<IBookingRepository>?     bookingRepo  = null,
        Mock<IScheduleSlotRepository>? slotRepo    = null,
        Mock<IUserRepository>?        userRepo     = null,
        Mock<IUnitOfWork>?            unitOfWork   = null,
        Mock<IEmailService>?          emailService = null,
        Mock<ITimeProvider>?          timeProvider = null)
    {
        paymentRepo  ??= TestMocks.PaymentRepo();
        bookingRepo  ??= TestMocks.BookingRepo();
        slotRepo     ??= TestMocks.SlotRepo();
        userRepo     ??= TestMocks.UserRepo();
        unitOfWork   ??= TestMocks.UnitOfWork();
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
            TestMocks.StripeRefund().Object,
            timeProvider.Object);
    }

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmFromWebhookAsync_PaymentNotFound_ThrowsKeyNotFoundException()
    {
        var paymentRepo = TestMocks.PaymentRepo();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId))
                   .ReturnsAsync((PaymentModel?)null);

        var sut = CreateSut(paymentRepo: paymentRepo);

        await sut.Invoking(s => s.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage($"*{StripeSessionId}*");
    }

    [Fact]
    public async Task ConfirmFromWebhookAsync_BookingNotFound_ThrowsKeyNotFoundException()
    {
        var paymentRepo = TestMocks.PaymentRepo();
        var bookingRepo = TestMocks.BookingRepo();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId))
                   .ReturnsAsync(PaidPayment());
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId))
                   .ReturnsAsync((BookingModel?)null);

        var sut = CreateSut(paymentRepo: paymentRepo, bookingRepo: bookingRepo);

        await sut.Invoking(s => s.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage($"*{_bookingId}*");
    }

    [Fact]
    public async Task ConfirmFromWebhookAsync_SlotNotFound_ThrowsKeyNotFoundException()
    {
        var paymentRepo = TestMocks.PaymentRepo();
        var bookingRepo = TestMocks.BookingRepo();
        var slotRepo    = TestMocks.SlotRepo();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId))
                   .ReturnsAsync(PaidPayment());
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId))
                   .ReturnsAsync(ConfirmedBooking());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId))
                .ReturnsAsync((ScheduleSlotModel?)null);

        var sut = CreateSut(paymentRepo: paymentRepo, bookingRepo: bookingRepo, slotRepo: slotRepo);

        await sut.Invoking(s => s.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage($"*{_slotId}*");
    }

    // ── happy-path tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmFromWebhookAsync_HappyPath_BookingConfirmedAndPaymentUpdated()
    {
        var paymentRepo = TestMocks.PaymentRepo();
        var bookingRepo = TestMocks.BookingRepo();
        var slotRepo    = TestMocks.SlotRepo();
        var uow         = TestMocks.UnitOfWork();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId)).ReturnsAsync(PaidPayment());
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(ConfirmedBooking());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(OnlineSlot());

        var sut = CreateSut(paymentRepo: paymentRepo, bookingRepo: bookingRepo,
                            slotRepo: slotRepo, unitOfWork: uow);

        await sut.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId);

        bookingRepo.Verify(r => r.UpdateStatusAsync(_bookingId, BookingStatus.Confirmed), Times.Once);
        paymentRepo.Verify(r => r.UpdateOnSuccessAsync(_bookingId, PaymentIntentId, _frozenNow), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmFromWebhookAsync_HappyPath_ReceiptEmailSentWithCorrectData()
    {
        var paymentRepo  = TestMocks.PaymentRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var userRepo     = TestMocks.UserRepo();
        var emailService = TestMocks.EmailService();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId)).ReturnsAsync(PaidPayment());
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(ConfirmedBooking());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(OnlineSlot());
        userRepo.Setup(r => r.GetByIdAsync(_clientId)).ReturnsAsync(ClientUser());
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        ReceiptData? capturedReceipt = null;
        emailService.Setup(e => e.SendBookingReceiptAsync(It.IsAny<string>(), It.IsAny<ReceiptData>()))
                    .Callback<string, ReceiptData>((_, r) => capturedReceipt = r);

        var sut = CreateSut(paymentRepo: paymentRepo, bookingRepo: bookingRepo,
                            slotRepo: slotRepo, userRepo: userRepo, emailService: emailService);

        await sut.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId);

        emailService.Verify(
            e => e.SendBookingReceiptAsync("anna@test.com", It.IsAny<ReceiptData>()),
            Times.Once);

        capturedReceipt!.ClientName.Should().Be("Anna Koval");
        capturedReceipt.TrainerName.Should().Be("John Smith");
        capturedReceipt.Amount.Should().Be(500m);
        capturedReceipt.Currency.Should().Be("UAH");
        capturedReceipt.PaymentIntentId.Should().Be(PaymentIntentId);
        capturedReceipt.PaidAt.Should().Be(_frozenNow);
    }

    [Fact]
    public async Task ConfirmFromWebhookAsync_OnlineSlot_ReceiptSessionFormatIsOnline()
    {
        var slot = OnlineSlot();
        slot.Format = SlotFormat.Online;

        var paymentRepo  = TestMocks.PaymentRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var userRepo     = TestMocks.UserRepo();
        var emailService = TestMocks.EmailService();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId)).ReturnsAsync(PaidPayment());
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(ConfirmedBooking());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(slot);
        userRepo.Setup(r => r.GetByIdAsync(_clientId)).ReturnsAsync(ClientUser());
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        ReceiptData? capturedReceipt = null;
        emailService.Setup(e => e.SendBookingReceiptAsync(It.IsAny<string>(), It.IsAny<ReceiptData>()))
                    .Callback<string, ReceiptData>((_, r) => capturedReceipt = r);

        var sut = CreateSut(paymentRepo: paymentRepo, bookingRepo: bookingRepo,
                            slotRepo: slotRepo, userRepo: userRepo, emailService: emailService);

        await sut.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId);

        capturedReceipt!.SessionFormat.Should().Be("Online");
    }

    [Fact]
    public async Task ConfirmFromWebhookAsync_OfflineSlot_ReceiptSessionFormatIsOffline()
    {
        var slot = OnlineSlot();
        slot.Format = SlotFormat.Offline;

        var paymentRepo  = TestMocks.PaymentRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var userRepo     = TestMocks.UserRepo();
        var emailService = TestMocks.EmailService();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId)).ReturnsAsync(PaidPayment());
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(ConfirmedBooking());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(slot);
        userRepo.Setup(r => r.GetByIdAsync(_clientId)).ReturnsAsync(ClientUser());
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        ReceiptData? capturedReceipt = null;
        emailService.Setup(e => e.SendBookingReceiptAsync(It.IsAny<string>(), It.IsAny<ReceiptData>()))
                    .Callback<string, ReceiptData>((_, r) => capturedReceipt = r);

        var sut = CreateSut(paymentRepo: paymentRepo, bookingRepo: bookingRepo,
                            slotRepo: slotRepo, userRepo: userRepo, emailService: emailService);

        await sut.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId);

        capturedReceipt!.SessionFormat.Should().Be("Offline");
    }

    [Fact]
    public async Task ConfirmFromWebhookAsync_ClientNotFound_NoEmailSent()
    {
        var paymentRepo  = TestMocks.PaymentRepo();
        var bookingRepo  = TestMocks.BookingRepo();
        var slotRepo     = TestMocks.SlotRepo();
        var userRepo     = TestMocks.UserRepo();
        var emailService = TestMocks.EmailService();
        paymentRepo.Setup(r => r.GetByStripeSessionIdAsync(StripeSessionId)).ReturnsAsync(PaidPayment());
        bookingRepo.Setup(r => r.GetByIdAsync(_bookingId)).ReturnsAsync(ConfirmedBooking());
        slotRepo.Setup(r => r.GetByIdAsync(_slotId)).ReturnsAsync(OnlineSlot());
        userRepo.Setup(r => r.GetByIdAsync(_clientId)).ReturnsAsync((UserModel?)null);

        var sut = CreateSut(paymentRepo: paymentRepo, bookingRepo: bookingRepo,
                            slotRepo: slotRepo, userRepo: userRepo, emailService: emailService);

        await sut.ConfirmFromWebhookAsync(StripeSessionId, PaymentIntentId);

        emailService.Verify(
            e => e.SendBookingReceiptAsync(It.IsAny<string>(), It.IsAny<ReceiptData>()),
            Times.Never);
    }
}
