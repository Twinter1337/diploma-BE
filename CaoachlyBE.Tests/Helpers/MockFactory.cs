using CaoachlyBE.Helpers;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;

namespace CaoachlyBE.Tests.Helpers;

/// <summary>
/// Central place for creating pre-wired mocks so each test class only overrides what it cares about.
/// </summary>
public static class TestMocks
{
    public static Mock<IBookingRepository> BookingRepo() => new();
    public static Mock<IPaymentRepository> PaymentRepo() => new();
    public static Mock<IScheduleSlotRepository> SlotRepo() => new();
    public static Mock<IUserRepository> UserRepo() => new();
    public static Mock<IUnitOfWork> UnitOfWork() => new();
    public static Mock<IEmailService> EmailService() => new();
    public static Mock<IStripeCheckoutService> StripeCheckout() => new();
    public static Mock<IStripeRefundService> StripeRefund() => new();
    public static Mock<ITrainerInfoRepository> TrainerInfoRepo() => new();
    public static Mock<IScheduleSlotRepository> ScheduleSlotRepo() => new();
    public static Mock<ITrainerDocumentRepository> TrainerDocumentRepo() => new();
    public static Mock<INotificationRepository> NotificationRepo() => new();
    public static Mock<ISupportTicketRepository> SupportTicketRepo() => new();
    public static Mock<IBlobStorageService> BlobStorageService() => new();
    public static Mock<IBookingRepository> BookingRepository() => new();
    public static Mock<IPaymentRepository> PaymentRepository() => new();

    /// <summary>Returns a time provider frozen at the given UTC moment (converted to UA time).</summary>
    public static Mock<ITimeProvider> TimeAt(DateTime utcNow)
    {
        var mock = new Mock<ITimeProvider>();
        // UA is UTC+3; callers can pass any DateTime they want directly as "UA now"
        mock.Setup(t => t.Now).Returns(utcNow);
        return mock;
    }

    public static IConfiguration DefaultConfiguration(
        string successUrl = "https://pay.example.com/success",
        string cancelUrl = "https://pay.example.com/cancel",
        int reservationWindowMinutes = 15,
        int lateFeePercent = 20)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Stripe:SuccessUrl"] = successUrl,
            ["Stripe:CancelUrl"] = cancelUrl,
            ["Booking:ReservationWindowMinutes"] = reservationWindowMinutes.ToString(),
            ["Booking:LateFeePercent"] = lateFeePercent.ToString(),
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }
}
