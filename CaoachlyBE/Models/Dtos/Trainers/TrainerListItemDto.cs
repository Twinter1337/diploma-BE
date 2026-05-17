using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tags;

namespace CaoachlyBE.Models.Dtos.Trainers;

public class TrainerListItemDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public VerificationStatus VerificationStatus { get; set; }
    public bool IsAccessible { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
    public decimal? MinPrice { get; set; }
    public List<TagListItemDto> SpecializationTags { get; set; } = [];
    public List<TagListItemDto> DisabilityTags { get; set; } = [];
    public List<TagListItemDto> MethodologyTags { get; set; } = [];
    public string? City { get; set; }
    public string? AvatarUrl { get; set; }
}
