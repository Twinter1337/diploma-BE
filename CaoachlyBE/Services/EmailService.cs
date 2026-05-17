using CaoachlyBE.Models;
using CaoachlyBE.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CaoachlyBE.Services;

public class EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = settings.Value;

    public async Task SendBookingReceiptAsync(string toEmail, ReceiptData data)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"Payment confirmed — {FormatAmount(data.Amount, data.Currency)}";
            message.Body = new TextPart("html") { Text = BuildHtml(toEmail, data) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Receipt sent to {Email} for payment {PaymentId}.", toEmail, data.PaymentIntentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send receipt to {Email} for payment {PaymentId}.", toEmail, data.PaymentIntentId);
        }
    }

    private string BuildHtml(string toEmail, ReceiptData data)
    {
        var amount = FormatAmount(data.Amount, data.Currency);
        var currency = data.Currency.ToUpperInvariant();
        var sessionTime = $"{data.SessionStartTime:dd MMM yyyy, HH:mm} — {data.SessionEndTime:HH:mm}";
        var paidAt = data.PaidAt.ToString("MMMM d, yyyy 'at' h:mm tt UTC");
        var businessName = _settings.BusinessName;

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f0f2f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f2f5;padding:40px 16px;">
            <tr><td align="center">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

                <!-- Header -->
                <tr>
                  <td style="background:#6366f1;padding:28px 36px;">
                    <span style="font-size:20px;font-weight:700;color:#ffffff;">{businessName}</span>
                  </td>
                </tr>

                <!-- Success badge -->
                <tr>
                  <td align="center" style="padding:36px 36px 8px;">
                    <div style="display:inline-block;background:#dcfce7;border-radius:50%;width:64px;height:64px;line-height:64px;text-align:center;font-size:32px;">✓</div>
                    <h1 style="margin:16px 0 4px;font-size:22px;font-weight:700;color:#111827;">Payment Successful</h1>
                    <p style="margin:0;color:#6b7280;font-size:14px;">Your session has been confirmed. See you there!</p>
                  </td>
                </tr>

                <!-- Amount -->
                <tr>
                  <td align="center" style="padding:24px 36px;">
                    <div style="background:#f9fafb;border-radius:10px;padding:20px 32px;display:inline-block;">
                      <span style="font-size:40px;font-weight:800;color:#111827;">{amount}</span>
                      <span style="font-size:18px;font-weight:600;color:#6b7280;margin-left:6px;">{currency}</span>
                    </div>
                  </td>
                </tr>

                <!-- Details table -->
                <tr>
                  <td style="padding:0 36px 32px;">
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb;">
                      {Row("Trainer", data.TrainerName)}
                      {Row("Session", sessionTime)}
                      {Row("Format", data.SessionFormat)}
                      {Row("Payment ID", $"<span style='font-family:monospace;font-size:12px;color:#374151;'>{data.PaymentIntentId}</span>")}
                      {Row("Date paid", paidAt)}
                      {Row("Receipt sent to", toEmail)}
                    </table>
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f9fafb;border-top:1px solid #e5e7eb;padding:20px 36px;text-align:center;">
                    <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">Thank you for booking with {businessName}.</p>
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {businessName}. All rights reserved.</p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string Row(string label, string value) => $"""
        <tr>
          <td style="padding:14px 0 0;font-size:13px;color:#9ca3af;font-weight:500;width:40%;vertical-align:top;">{label}</td>
          <td style="padding:14px 0 0;font-size:13px;color:#111827;font-weight:500;text-align:right;">{value}</td>
        </tr>
        """;

    public async Task SendReviewRequestAsync(string toEmail, ReviewRequestData data)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"How was your session with {data.TrainerFullName}?";
            message.Body = new TextPart("html") { Text = BuildReviewRequestHtml(data) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Review request sent to {Email} for booking {BookingId}.", toEmail, data.BookingId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send review request to {Email} for booking {BookingId}.", toEmail, data.BookingId);
        }
    }

    private string BuildReviewRequestHtml(ReviewRequestData data)
    {
        var sessionTime = $"{data.SessionStartTime:dd MMM yyyy, HH:mm} — {data.SessionEndTime:HH:mm}";
        var businessName = _settings.BusinessName;

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f0f2f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f2f5;padding:40px 16px;">
            <tr><td align="center">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

                <!-- Header -->
                <tr>
                  <td style="background:#6366f1;padding:28px 36px;">
                    <span style="font-size:20px;font-weight:700;color:#ffffff;">{businessName}</span>
                  </td>
                </tr>

                <!-- Star badge -->
                <tr>
                  <td align="center" style="padding:36px 36px 8px;">
                    <div style="display:inline-block;background:#fef9c3;border-radius:50%;width:64px;height:64px;line-height:64px;text-align:center;font-size:32px;">⭐</div>
                    <h1 style="margin:16px 0 4px;font-size:22px;font-weight:700;color:#111827;">Session Complete!</h1>
                    <p style="margin:0;color:#6b7280;font-size:14px;">Hi {data.ClientFirstName}, how did it go?</p>
                  </td>
                </tr>

                <!-- Session details -->
                <tr>
                  <td style="padding:24px 36px 8px;">
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb;">
                      {Row("Trainer", data.TrainerFullName)}
                      {Row("Session", sessionTime)}
                    </table>
                  </td>
                </tr>

                <!-- CTA -->
                <tr>
                  <td align="center" style="padding:28px 36px 36px;">
                    <p style="margin:0 0 20px;font-size:14px;color:#6b7280;">Your feedback helps other clients find great trainers and motivates coaches to keep improving.</p>
                    <a href="{_settings.FrontendBaseUrl}/review?bookingId={data.BookingId}" style="display:inline-block;background:#6366f1;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;padding:14px 32px;border-radius:8px;">Leave a Review</a>
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f9fafb;border-top:1px solid #e5e7eb;padding:20px 36px;text-align:center;">
                    <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">Thank you for training with {businessName}.</p>
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {businessName}. All rights reserved.</p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    public async Task SendRefundNotificationAsync(string toEmail, RefundNotificationData data)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"Booking cancelled — refund of {FormatAmount(data.RefundAmount, data.Currency)} is on its way";
            message.Body = new TextPart("html") { Text = BuildRefundHtml(data) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Refund notification sent to {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send refund notification to {Email}.", toEmail);
        }
    }

    private string BuildRefundHtml(RefundNotificationData data)
    {
        var refundAmount = FormatAmount(data.RefundAmount, data.Currency);
        var sessionTime = $"{data.SessionStartTime:ddd, MMM d · h:mm tt} – {data.SessionEndTime:h:mm tt} UTC";
        var cancelledAt = data.CancelledAt.ToString("MMM d, yyyy 'at' h:mm tt UTC");

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;padding:40px 0;">
            <tr><td align="center">
              <table width="520" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;max-width:520px;">
                <tr>
                  <td style="background:#ef4444;padding:28px 32px;text-align:center;">
                    <p style="margin:0;font-size:13px;font-weight:600;letter-spacing:.08em;color:#fecaca;text-transform:uppercase;">{_settings.BusinessName}</p>
                    <h1 style="margin:8px 0 0;font-size:22px;font-weight:700;color:#ffffff;">Booking Cancelled</h1>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px 32px 0;text-align:center;">
                    <div style="display:inline-block;background:#fef2f2;border:2px solid #fecaca;border-radius:50%;width:64px;height:64px;line-height:64px;font-size:30px;">💸</div>
                    <p style="margin:16px 0 4px;font-size:14px;color:#6b7280;">Refund amount</p>
                    <p style="margin:0;font-size:36px;font-weight:800;color:#111827;">{refundAmount}</p>
                    <p style="margin:6px 0 0;font-size:13px;color:#6b7280;">{data.RefundPercentage}% refund applied</p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:24px 32px 0;">
                    <p style="margin:0 0 16px;font-size:15px;color:#374151;">Hi {data.ClientFirstName},</p>
                    <p style="margin:0 0 24px;font-size:15px;color:#374151;line-height:1.6;">
                      Your booking has been cancelled and a <strong>{data.RefundPercentage}% refund</strong> of <strong>{refundAmount}</strong> has been initiated.
                      Please allow <strong>3–5 business days</strong> for the amount to appear on your statement.
                    </p>
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb;">
                      {Row("Trainer", data.TrainerName)}
                      {Row("Session", sessionTime)}
                      {Row("Refund %", $"{data.RefundPercentage}%")}
                      {Row("Cancelled at", cancelledAt)}
                    </table>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px;text-align:center;border-top:1px solid #e5e7eb;margin-top:24px;">
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {_settings.BusinessName}. All rights reserved.</p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    public async Task SendSlotUpdateNotificationAsync(string toEmail, SlotUpdateNotificationData data)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"Your session with {data.TrainerFullName} has been updated";
            message.Body = new TextPart("html") { Text = BuildSlotUpdateHtml(data) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Slot update notification sent to {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send slot update notification to {Email}.", toEmail);
        }
    }

    private string BuildSlotUpdateHtml(SlotUpdateNotificationData data)
    {
        var businessName = _settings.BusinessName;

        var changeRows = string.Join("\n", data.Changes.Select(c => $"""
            <tr>
              <td style="padding:12px 0 0;font-size:13px;color:#9ca3af;font-weight:500;width:30%;vertical-align:top;">{c.Field}</td>
              <td style="padding:12px 0 0;font-size:13px;color:#ef4444;text-decoration:line-through;text-align:center;width:30%;vertical-align:top;">{c.Before}</td>
              <td style="padding:12px 0 0;font-size:13px;color:#16a34a;font-weight:600;text-align:right;width:40%;vertical-align:top;">{c.After}</td>
            </tr>
            """));

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f0f2f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f2f5;padding:40px 16px;">
            <tr><td align="center">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

                <!-- Header -->
                <tr>
                  <td style="background:#6366f1;padding:28px 36px;">
                    <span style="font-size:20px;font-weight:700;color:#ffffff;">{businessName}</span>
                  </td>
                </tr>

                <!-- Badge -->
                <tr>
                  <td align="center" style="padding:36px 36px 8px;">
                    <div style="display:inline-block;background:#eff6ff;border-radius:50%;width:64px;height:64px;line-height:64px;text-align:center;font-size:32px;">📋</div>
                    <h1 style="margin:16px 0 4px;font-size:22px;font-weight:700;color:#111827;">Session Updated</h1>
                    <p style="margin:0;color:#6b7280;font-size:14px;">Hi {data.ClientFirstName}, your coach has made changes to a session you booked.</p>
                  </td>
                </tr>

                <!-- Change table -->
                <tr>
                  <td style="padding:24px 36px 8px;">
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb;">
                      <tr>
                        <td style="padding:10px 0 0;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:.06em;color:#9ca3af;width:30%;">Field</td>
                        <td style="padding:10px 0 0;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:.06em;color:#9ca3af;text-align:center;width:30%;">Before</td>
                        <td style="padding:10px 0 0;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:.06em;color:#9ca3af;text-align:right;width:40%;">After</td>
                      </tr>
                      {changeRows}
                    </table>
                  </td>
                </tr>

                <!-- Trainer row -->
                <tr>
                  <td style="padding:16px 36px 32px;">
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb;">
                      {Row("Coach", data.TrainerFullName)}
                    </table>
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f9fafb;border-top:1px solid #e5e7eb;padding:20px 36px;text-align:center;">
                    <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">If you have questions, please contact your coach directly.</p>
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {businessName}. All rights reserved.</p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    public async Task SendSessionReminderAsync(string toEmail, SessionReminderData data)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"Session Reminder — {data.StartTime:dd MMM yyyy, HH:mm}";
            message.Body = new TextPart("html") { Text = BuildSessionReminderHtml(data) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Session reminder sent to {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send session reminder to {Email}.", toEmail);
        }
    }

    private string BuildSessionReminderHtml(SessionReminderData data)
    {
        var businessName = _settings.BusinessName;
        var sessionTime = data.StartTime.ToString("dddd, dd MMM yyyy 'at' HH:mm");
        var (heading, subheading) = data.IsTrainer
            ? ("Upcoming Session", $"Hi {data.RecipientFirstName}, you have a session with {data.ClientFullName} coming up.")
            : ("Session Reminder", $"Hi {data.RecipientFirstName}, your session with {data.TrainerFullName} is coming up.");

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f0f2f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f2f5;padding:40px 16px;">
            <tr><td align="center">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

                <!-- Header -->
                <tr>
                  <td style="background:#6366f1;padding:28px 36px;">
                    <span style="font-size:20px;font-weight:700;color:#ffffff;">{businessName}</span>
                  </td>
                </tr>

                <!-- Icon + heading -->
                <tr>
                  <td align="center" style="padding:36px 36px 8px;">
                    <div style="display:inline-block;background:#eff6ff;border-radius:50%;width:64px;height:64px;line-height:64px;text-align:center;font-size:32px;">🔔</div>
                    <h1 style="margin:16px 0 4px;font-size:22px;font-weight:700;color:#111827;">{heading}</h1>
                    <p style="margin:0;color:#6b7280;font-size:14px;">{subheading}</p>
                  </td>
                </tr>

                <!-- Details -->
                <tr>
                  <td style="padding:24px 36px 32px;">
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb;">
                      {Row("Trainer", data.TrainerFullName)}
                      {Row("Client", data.ClientFullName)}
                      {Row("Starts at", sessionTime)}
                    </table>
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f9fafb;border-top:1px solid #e5e7eb;padding:20px 36px;text-align:center;">
                    <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">See you at the session!</p>
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {businessName}. All rights reserved.</p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    public async Task SendSlotCancelledNotificationAsync(string toEmail, SlotCancelledNotificationData data)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"Your session with {data.TrainerFullName} has been cancelled";
            message.Body = new TextPart("html") { Text = BuildSlotCancelledHtml(data) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Slot cancelled notification sent to {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send slot cancelled notification to {Email}.", toEmail);
        }
    }

    private string BuildSlotCancelledHtml(SlotCancelledNotificationData data)
    {
        var sessionTime = $"{data.SessionStartTime:ddd, MMM d · h:mm tt} – {data.SessionEndTime:h:mm tt} UTC";
        var cancelledAt = data.CancelledAt.ToString("MMM d, yyyy 'at' h:mm tt UTC");

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;padding:40px 0;">
            <tr><td align="center">
              <table width="520" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;max-width:520px;">
                <tr>
                  <td style="background:#ef4444;padding:28px 32px;text-align:center;">
                    <p style="margin:0;font-size:13px;font-weight:600;letter-spacing:.08em;color:#fecaca;text-transform:uppercase;">{_settings.BusinessName}</p>
                    <h1 style="margin:8px 0 0;font-size:22px;font-weight:700;color:#ffffff;">Session Cancelled</h1>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px 32px 0;text-align:center;">
                    <div style="display:inline-block;background:#fef2f2;border:2px solid #fecaca;border-radius:50%;width:64px;height:64px;line-height:64px;font-size:30px;">❌</div>
                  </td>
                </tr>
                <tr>
                  <td style="padding:24px 32px 0;">
                    <p style="margin:0 0 16px;font-size:15px;color:#374151;">Hi {data.ClientFirstName},</p>
                    <p style="margin:0 0 24px;font-size:15px;color:#374151;line-height:1.6;">
                      We're sorry to let you know that your upcoming session with <strong>{data.TrainerFullName}</strong> has been cancelled by the trainer.
                      No payment was collected, so no refund is necessary.
                    </p>
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb;">
                      {Row("Trainer", data.TrainerFullName)}
                      {Row("Session", sessionTime)}
                      {Row("Cancelled at", cancelledAt)}
                    </table>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px;text-align:center;border-top:1px solid #e5e7eb;margin-top:24px;">
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {_settings.BusinessName}. All rights reserved.</p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetUrl, string firstName)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Reset your password";
            message.Body = new TextPart("html") { Text = BuildPasswordResetHtml(firstName, resetUrl) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Password reset email sent to {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to {Email}.", toEmail);
        }
    }

    private string BuildPasswordResetHtml(string firstName, string resetUrl)
    {
        var businessName = _settings.BusinessName;
        var greetingName = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f0f2f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f2f5;padding:40px 16px;">
            <tr><td align="center">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">
                <tr>
                  <td style="background:#6366f1;padding:28px 36px;">
                    <span style="font-size:20px;font-weight:700;color:#ffffff;">{businessName}</span>
                  </td>
                </tr>
                <tr>
                  <td style="padding:36px 36px 8px;">
                    <h1 style="margin:0 0 12px;font-size:22px;font-weight:700;color:#111827;">Reset your password</h1>
                    <p style="margin:0 0 16px;color:#374151;font-size:15px;line-height:1.6;">Hi {greetingName},</p>
                    <p style="margin:0 0 16px;color:#374151;font-size:15px;line-height:1.6;">We received a request to reset the password for your {businessName} account. Click the button below to choose a new password. This link expires in 48 hours.</p>
                  </td>
                </tr>
                <tr>
                  <td align="center" style="padding:8px 36px 32px;">
                    <a href="{resetUrl}" style="display:inline-block;background:#6366f1;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:14px 28px;border-radius:8px;">Reset password</a>
                  </td>
                </tr>
                <tr>
                  <td style="padding:0 36px 32px;">
                    <p style="margin:0 0 8px;color:#6b7280;font-size:13px;line-height:1.6;">If the button doesn't work, paste this link into your browser:</p>
                    <p style="margin:0;word-break:break-all;color:#6366f1;font-size:13px;">{resetUrl}</p>
                    <p style="margin:24px 0 0;color:#9ca3af;font-size:12px;line-height:1.6;">If you didn't request a password reset, you can safely ignore this email — your password won't change.</p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:24px 36px;text-align:center;border-top:1px solid #e5e7eb;">
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {businessName}. All rights reserved.</p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    public async Task SendAdminReplyAsync(string toEmail, string subject, string body)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = BuildAdminReplyHtml(body) };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Admin reply sent to {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send admin reply to {Email}.", toEmail);
            throw;
        }
    }

    private string BuildAdminReplyHtml(string body)
    {
        var businessName = _settings.BusinessName;
        var safeBody = body.Replace("\n", "<br/>");
        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f0f2f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f2f5;padding:40px 16px;">
            <tr><td align="center">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">
                <tr>
                  <td style="background:#6366f1;padding:28px 36px;">
                    <span style="font-size:20px;font-weight:700;color:#ffffff;">{businessName}</span>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px 36px;color:#374151;font-size:15px;line-height:1.7;">
                    {safeBody}
                  </td>
                </tr>
                <tr>
                  <td style="padding:24px 36px;text-align:center;border-top:1px solid #e5e7eb;">
                    <p style="margin:0;font-size:12px;color:#9ca3af;">© {DateTime.UtcNow.Year} {businessName}. All rights reserved.</p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string FormatAmount(decimal amount, string currency) =>
        currency.ToUpperInvariant() switch
        {
            "UAH" => $"₴{amount:N2}",
            "USD" => $"${amount:N2}",
            "EUR" => $"€{amount:N2}",
            "GBP" => $"£{amount:N2}",
            _ => $"{amount:N2}"
        };
}
