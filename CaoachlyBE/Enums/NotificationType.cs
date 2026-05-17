namespace CaoachlyBE.Enums;

public enum NotificationType : short
{
    BookingConfirmed = 0,
    BookingCancelled = 1,
    PaymentSuccess = 2,
    PaymentRefunded = 3,
    VerificationApproved = 4,
    VerificationRejected = 5,
    SessionReminder = 6,
    ReviewRequest = 7
}
