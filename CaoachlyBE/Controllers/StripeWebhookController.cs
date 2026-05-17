using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController(
    IBookingService bookingService,
    IConfiguration configuration,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [Microsoft.AspNetCore.Mvc.DisableRequestSizeLimit]
    public async Task<IActionResult> Handle()
    {
        Request.EnableBuffering();
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        var webhookSecret = configuration["Stripe:WebhookSecret"]!;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe webhook signature validation failed: {Message}", ex.Message);
            return BadRequest();
        }

        logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Session;
            if (session is not null)
            {
                try
                {
                    await bookingService.ConfirmFromWebhookAsync(session.Id, session.PaymentIntentId);
                    logger.LogInformation("Booking confirmed for Stripe session {SessionId}.", session.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to confirm booking for session {SessionId}.", session.Id);
                    // Return 200 so Stripe does not retry — error is logged for investigation
                }
            }
        }

        return Ok();
    }
}
