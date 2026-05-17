using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Trainers;

public class TrainerSearchFilterDto
{
    public List<int>? SpecializationTagIds { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [Range(0, 99999.99)]
    public decimal? MinPrice { get; set; }

    [Range(0, 99999.99)]
    public decimal? MaxPrice { get; set; }

    [Range(0, 5)]
    public decimal? MinRating { get; set; }

    [MaxLength(100)]
    public string? Name { get; set; }

    public bool? IsVerified { get; set; }
    public bool? IsAccess { get; set; }
    public List<int>? MethodologyTagIds { get; set; }
    public List<int>? DisabilityTagIds { get; set; }
}
