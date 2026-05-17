using CaoachlyBE.Models;

namespace CaoachlyBE.Services.Interfaces;

public interface IEmailService
{
    Task SendBookingReceiptAsync(string toEmail, ReceiptData data);
    Task SendReviewRequestAsync(string toEmail, ReviewRequestData data);
    Task SendRefundNotificationAsync(string toEmail, RefundNotificationData data);
    Task SendSlotUpdateNotificationAsync(string toEmail, SlotUpdateNotificationData data);
    Task SendSessionReminderAsync(string toEmail, SessionReminderData data);
    Task SendSlotCancelledNotificationAsync(string toEmail, SlotCancelledNotificationData data);
    Task SendPasswordResetAsync(string toEmail, string resetUrl, string firstName);
    Task SendAdminReplyAsync(string toEmail, string subject, string body);
}
