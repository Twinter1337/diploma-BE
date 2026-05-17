using System.ComponentModel.DataAnnotations;
using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Tags;

public class CreateTagDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public TagCategory Category { get; set; }

    public string? Description { get; set; }
}
