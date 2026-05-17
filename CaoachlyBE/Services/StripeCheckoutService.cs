using CaoachlyBE.Services.Interfaces;
using Stripe.Checkout;

namespace CaoachlyBE.Services;

public class StripeCheckoutService : IStripeCheckoutService
{
    private readonly SessionService _sessionService = new();

    public Task<Session> CreateAsync(SessionCreateOptions options) =>
        _sessionService.CreateAsync(options);

    public Task<Session> GetAsync(string sessionId) =>
        _sessionService.GetAsync(sessionId);

    public Task ExpireAsync(string sessionId) =>
        _sessionService.ExpireAsync(sessionId);
}
