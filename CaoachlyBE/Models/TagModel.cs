using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class TagModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public TagCategory Category { get; set; }
    public string? Description { get; set; }
}
