using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class ClientInfo
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public short? HeightCm { get; set; }

    public decimal? WeightKg { get; set; }

    public string? FitnessGoals { get; set; }

    public virtual User User { get; set; } = null!;
}
