using CaoachlyBE.Services.Interfaces;
using Stripe;

namespace CaoachlyBE.Services;

public class StripeRefundService : IStripeRefundService
{
    private readonly RefundService _refundService = new();

    public Task CreateAsync(RefundCreateOptions options) =>
        _refundService.CreateAsync(options);
}
