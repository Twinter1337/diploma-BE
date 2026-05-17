using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tags;

namespace CaoachlyBE.Models.Dtos.Clients;

public class ClientProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? City { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? About { get; set; }
    public short? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public List<TagListItemDto> AccessTags { get; set; } = [];
}
