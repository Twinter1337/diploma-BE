using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Trainers;

public class UpdateTrainerInfoDto
{
    [MaxLength(2000)]
    public string? Bio { get; set; }

    [Range(0, 100)]
    public short? ExperienceYears { get; set; }
}
