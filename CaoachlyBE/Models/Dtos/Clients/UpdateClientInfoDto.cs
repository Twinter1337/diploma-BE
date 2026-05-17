using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Clients;

public class UpdateClientInfoDto
{
    [Range(50, 300)]
    public short? HeightCm { get; set; }

    [Range(1, 500)]
    public decimal? WeightKg { get; set; }

    [MaxLength(2000)]
    public string? FitnessGoals { get; set; }
}
