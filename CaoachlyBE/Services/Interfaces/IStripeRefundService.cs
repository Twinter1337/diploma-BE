using Stripe;

namespace CaoachlyBE.Services.Interfaces;

public interface IStripeRefundService
{
    Task CreateAsync(RefundCreateOptions options);
}
