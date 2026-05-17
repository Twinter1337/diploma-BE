using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public short Type { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public Guid? BookingId { get; set; }

    public DateTime SentAt { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual User User { get; set; } = null!;
}
