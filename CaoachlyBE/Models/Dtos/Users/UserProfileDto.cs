using CaoachlyBE.Models.Dtos.Tags;

namespace CaoachlyBE.Models.Dtos.Users;

public class UserProfileDto
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? City { get; set; }
    public short? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public List<DisabilityTagItemDto> DisabilityTags { get; set; } = [];
    public string? AvatarUrl { get; set; }
}
