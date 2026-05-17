using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class ScheduleSlot
{
    public Guid Id { get; set; }

    public Guid TrainerId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public short Format { get; set; }

    public decimal Price { get; set; }

    public short MaxClients { get; set; }

    public string? Description { get; set; }

    public string? GymName { get; set; }

    public string? GymAddress { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual User Trainer { get; set; } = null!;
}
