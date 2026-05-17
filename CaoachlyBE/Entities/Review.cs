using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class Review
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid ClientId { get; set; }

    public Guid TrainerId { get; set; }

    public short Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual User Client { get; set; } = null!;

    public virtual User Trainer { get; set; } = null!;
}
