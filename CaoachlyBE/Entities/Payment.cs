using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class Payment
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public short PaymentMethod { get; set; }

    public short Status { get; set; }

    public string? TransactionId { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
