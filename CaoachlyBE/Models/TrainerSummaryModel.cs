using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class TrainerSummaryModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? City { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
    public bool IsAccessible { get; set; }
    public List<TagModel> SpecializationTags { get; set; } = [];
    public List<TagModel> DisabilityTags { get; set; } = [];
    public List<TagModel> MethodologyTags { get; set; } = [];
    public decimal? MinSlotPrice { get; set; }
}
