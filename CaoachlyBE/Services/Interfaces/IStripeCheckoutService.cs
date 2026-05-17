using Stripe.Checkout;

namespace CaoachlyBE.Services.Interfaces;

public interface IStripeCheckoutService
{
    Task<Session> CreateAsync(SessionCreateOptions options);
    Task<Session> GetAsync(string sessionId);
    Task ExpireAsync(string sessionId);
}
