using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class TrainerInfo
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? Bio { get; set; }

    public short ExperienceYears { get; set; }

    public short VerificationStatus { get; set; }

    public decimal Rating { get; set; }

    public int ReviewsCount { get; set; }

    public virtual User User { get; set; } = null!;
}
