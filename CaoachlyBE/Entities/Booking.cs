using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class Booking
{
    public Guid Id { get; set; }

    public Guid SlotId { get; set; }

    public Guid ClientId { get; set; }

    public short Status { get; set; }

    public string? CancellationReason { get; set; }

    public short? CancelledBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? ReminderMinutes { get; set; }

    public virtual User Client { get; set; } = null!;

    public virtual Payment? Payment { get; set; }

    public virtual Review? Review { get; set; }

    public virtual ICollection<SessionNote> SessionNotes { get; set; } = new List<SessionNote>();

    public virtual ScheduleSlot Slot { get; set; } = null!;

    public virtual ICollection<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
}
