using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Clients;

public class OnboardClientRequestDto
{
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [MaxLength(50)]
    public string? LastName { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(2000)]
    public string? About { get; set; }

    [Range(50, 300)]
    public short? HeightCm { get; set; }

    [Range(1, 500)]
    public decimal? WeightKg { get; set; }

    [Range(0, 2)]
    public int? Gender { get; set; }

    public DateOnly? BirthDate { get; set; }

    public List<int>? AccessTagIds { get; set; }
}
