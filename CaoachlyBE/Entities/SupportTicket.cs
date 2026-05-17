using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class SupportTicket
{
    public Guid Id { get; set; }

    public Guid CreatedBy { get; set; }

    public string Subject { get; set; } = null!;

    public string Description { get; set; } = null!;

    public short Status { get; set; }

    public Guid? RelatedBookingId { get; set; }

    public Guid? RelatedDocumentId { get; set; }

    public Guid? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Booking? RelatedBooking { get; set; }

    public virtual TrainerDocument? RelatedDocument { get; set; }
}
