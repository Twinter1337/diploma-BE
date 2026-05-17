using System.ComponentModel.DataAnnotations;
using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Schedule;

public class CreateScheduleSlotDto
{
    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    public SlotFormat Format { get; set; }

    [Range(0.01, 99999.99)]
    public decimal PricePerSession { get; set; }

    [Range(1, 100)]
    public short MaxClients { get; set; } = 1;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? GymName { get; set; }

    [MaxLength(300)]
    public string? GymAddress { get; set; }
}
