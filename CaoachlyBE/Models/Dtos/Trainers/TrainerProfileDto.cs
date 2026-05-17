using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tags;

namespace CaoachlyBE.Models.Dtos.Trainers;

public class TrainerProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? City { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Bio { get; set; }
    public short ExperienceYears { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
    public List<TagListItemDto> SpecializationTags { get; set; } = [];
    public List<TagListItemDto> MethodologyTags { get; set; } = [];
    public List<TagListItemDto> AccessTags { get; set; } = [];
}
