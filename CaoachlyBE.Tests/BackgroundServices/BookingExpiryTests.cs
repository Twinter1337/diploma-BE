using CaoachlyBE.BackgroundServices;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe.Checkout;

namespace CaoachlyBE.Tests.BackgroundServices;

public class BookingExpiryTests
{
    private readonly DateTime _frozenNow = new(2026, 5, 17, 12, 0, 0);

    // ── service factory ───────────────────────────────────────────────────────
    private BookingExpiryBackgroundService CreateSut(
        Mock<IBookingRepository>?     bookingRepo  = null,
        Mock<IPaymentRepository>?     paymentRepo  = null,
        Mock<IStripeCheckoutService>? stripe       = null,
        Mock<IUnitOfWork>?            unitOfWork   = null,
        Mock<ITimeProvider>?          timeProvider = null)
    {
        bookingRepo  ??= TestMocks.BookingRepo();
        paymentRepo  ??= TestMocks.PaymentRepo();
        stripe       ??= TestMocks.StripeCheckout();
        unitOfWork   ??= TestMocks.UnitOfWork();
        timeProvider ??= TestMocks.TimeAt(_frozenNow);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IBookingRepository))).Returns(bookingRepo.Object);
        provider.Setup(p => p.GetService(typeof(IPaymentRepository))).Returns(paymentRepo.Object);
        provider.Setup(p => p.GetService(typeof(IStripeCheckoutService))).Returns(stripe.Object);
        provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(unitOfWork.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new BookingExpiryBackgroundService(
            scopeFactory.Object,
            timeProvider.Object,
            NullLogger<BookingExpiryBackgroundService>.Instance);
    }

    private static Session OpenSession(string id = "cs_session") =>
        new() { Id = id, Status = "open" };

    private static Session ExpiredSession(string id = "cs_session") =>
        new() { Id = id, Status = "expired" };

    // ── no-op when nothing is expired ─────────────────────────────────────────

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_NoExpiredBookings_UowNotCalledAndNoCancellation()
    {
        var bookingRepo = TestMocks.BookingRepo();
        var uow         = TestMocks.UnitOfWork();
        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([]);

        var sut = CreateSut(bookingRepo: bookingRepo, unitOfWork: uow);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        bookingRepo.Verify(r => r.CancelAsync(It.IsAny<Guid>(), It.IsAny<CancelledBy>(), It.IsAny<string>()), Times.Never);
    }

    // ── cutoff calculation ────────────────────────────────────────────────────

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_CutoffPassedToRepo_Is15MinutesBeforeNow()
    {
        var bookingRepo = TestMocks.BookingRepo();

        DateTime? capturedCutoff = null;
        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .Callback<DateTime>(c => capturedCutoff = c)
                   .ReturnsAsync([]);

        var sut = CreateSut(bookingRepo: bookingRepo);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        capturedCutoff.Should().Be(_frozenNow.AddMinutes(-15));
    }

    // ── stripe session handling ───────────────────────────────────────────────

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_OpenStripeSession_SessionExpireCalled()
    {
        var bookingId = Guid.NewGuid();
        var bookingRepo = TestMocks.BookingRepo();
        var stripe      = TestMocks.StripeCheckout();

        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([(bookingId, "cs_open_session")]);
        stripe.Setup(s => s.GetAsync("cs_open_session")).ReturnsAsync(OpenSession("cs_open_session"));

        var sut = CreateSut(bookingRepo: bookingRepo, stripe: stripe);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        stripe.Verify(s => s.ExpireAsync("cs_open_session"), Times.Once);
    }

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_AlreadyExpiredStripeSession_ExpireNotCalled()
    {
        var bookingId   = Guid.NewGuid();
        var bookingRepo = TestMocks.BookingRepo();
        var stripe      = TestMocks.StripeCheckout();

        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([(bookingId, "cs_expired_session")]);
        stripe.Setup(s => s.GetAsync("cs_expired_session")).ReturnsAsync(ExpiredSession("cs_expired_session"));

        var sut = CreateSut(bookingRepo: bookingRepo, stripe: stripe);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        stripe.Verify(s => s.ExpireAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_StripeThrows_BookingStillCancelledAndPaymentMarkedFailed()
    {
        var bookingId   = Guid.NewGuid();
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var stripe      = TestMocks.StripeCheckout();

        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([(bookingId, "cs_error_session")]);
        stripe.Setup(s => s.GetAsync("cs_error_session")).ThrowsAsync(new Exception("Stripe error"));

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo, stripe: stripe);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        bookingRepo.Verify(r => r.CancelAsync(bookingId, CancelledBy.System, It.IsAny<string>()), Times.Once);
        paymentRepo.Verify(r => r.MarkAsFailedAsync(bookingId), Times.Once);
    }

    // ── cancellation and payment failure ──────────────────────────────────────

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_OneExpiredBooking_CancelledBySystemWithReason()
    {
        var bookingId   = Guid.NewGuid();
        var bookingRepo = TestMocks.BookingRepo();
        var stripe      = TestMocks.StripeCheckout();

        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([(bookingId, "cs_session")]);
        stripe.Setup(s => s.GetAsync("cs_session")).ReturnsAsync(ExpiredSession());

        var sut = CreateSut(bookingRepo: bookingRepo, stripe: stripe);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        bookingRepo.Verify(
            r => r.CancelAsync(bookingId, CancelledBy.System, It.Is<string>(s => s.Contains("15 minutes"))),
            Times.Once);
    }

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_OneExpiredBooking_PaymentMarkedFailed()
    {
        var bookingId   = Guid.NewGuid();
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var stripe      = TestMocks.StripeCheckout();

        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([(bookingId, "cs_session")]);
        stripe.Setup(s => s.GetAsync("cs_session")).ReturnsAsync(ExpiredSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo, stripe: stripe);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        paymentRepo.Verify(r => r.MarkAsFailedAsync(bookingId), Times.Once);
    }

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_TwoExpiredBookings_BothCancelledAndMarkedFailed()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var bookingRepo = TestMocks.BookingRepo();
        var paymentRepo = TestMocks.PaymentRepo();
        var stripe      = TestMocks.StripeCheckout();

        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([(id1, "cs_s1"), (id2, "cs_s2")]);
        stripe.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync(ExpiredSession());

        var sut = CreateSut(bookingRepo: bookingRepo, paymentRepo: paymentRepo, stripe: stripe);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        bookingRepo.Verify(r => r.CancelAsync(id1, CancelledBy.System, It.IsAny<string>()), Times.Once);
        bookingRepo.Verify(r => r.CancelAsync(id2, CancelledBy.System, It.IsAny<string>()), Times.Once);
        paymentRepo.Verify(r => r.MarkAsFailedAsync(id1), Times.Once);
        paymentRepo.Verify(r => r.MarkAsFailedAsync(id2), Times.Once);
    }

    // ── unit of work ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpireUnpaidBookingsAsync_OneExpiredBooking_UowSavedOnce()
    {
        var bookingId   = Guid.NewGuid();
        var bookingRepo = TestMocks.BookingRepo();
        var uow         = TestMocks.UnitOfWork();
        var stripe      = TestMocks.StripeCheckout();

        bookingRepo.Setup(r => r.GetExpiredPendingAsync(It.IsAny<DateTime>()))
                   .ReturnsAsync([(bookingId, "cs_session")]);
        stripe.Setup(s => s.GetAsync("cs_session")).ReturnsAsync(ExpiredSession());

        var sut = CreateSut(bookingRepo: bookingRepo, unitOfWork: uow, stripe: stripe);

        await sut.ExpireUnpaidBookingsAsync(CancellationToken.None);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
