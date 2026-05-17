using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class TrainerPublicProfileModel
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public bool IsAccessible { get; set; }
    public string? AvatarUrl { get; set; }
    public short ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public decimal Rating { get; set; }
    public decimal? MinPrice { get; set; }
    public string? City { get; set; }
    public int NumOfReviews { get; set; }
    public List<TagModel> SpecializationTags { get; set; } = [];
    public int NumOfCompletedClasses { get; set; }
    public List<TagModel> MethodologyTags { get; set; } = [];
    public List<TagModel> DisabilityTags { get; set; } = [];
    public int NumOfActiveClients { get; set; }
}
