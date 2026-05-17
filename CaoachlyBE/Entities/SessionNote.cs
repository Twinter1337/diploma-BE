using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class SessionNote
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid AuthorId { get; set; }

    public string Content { get; set; } = null!;

    public bool IsPrivate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Author { get; set; } = null!;

    public virtual Booking Booking { get; set; } = null!;
}
