using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Trainers;

public class OnboardTrainerRequestDto
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [Range(0, 2)]
    public int? Gender { get; set; }

    public DateOnly? BirthDate { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }

    [Range(0, 100)]
    public short? ExperienceYears { get; set; }

    public List<int>? SpecializationTagIds { get; set; }

    public List<int>? MethodologyTagIds { get; set; }

    public bool? HasAccess { get; set; }

    public List<int>? AccessTagIds { get; set; }
}
