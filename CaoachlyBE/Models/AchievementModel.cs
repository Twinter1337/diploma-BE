using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class AchievementModel
{
    public int Id { get; set; }
    public AchievementType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconUrl { get; set; } = null!;
}
