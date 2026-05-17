using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class UserAchievement
{
    public Guid UserId { get; set; }

    public int AchievementId { get; set; }

    public DateTime EarnedAt { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
