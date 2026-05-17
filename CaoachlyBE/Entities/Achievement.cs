using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class Achievement
{
    public int Id { get; set; }

    public short Type { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string IconUrl { get; set; } = null!;

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
