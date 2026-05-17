using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Tags;

public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public TagCategory Category { get; set; }
    public string? Description { get; set; }
}
