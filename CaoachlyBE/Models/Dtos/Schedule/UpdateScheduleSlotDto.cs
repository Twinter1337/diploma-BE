using System.ComponentModel.DataAnnotations;
using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Schedule;

public class UpdateScheduleSlotDto
{
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public SlotFormat? Format { get; set; }

    [Range(0.01, 99999.99)]
    public decimal? Price { get; set; }

    [Range(1, 100)]
    public short? MaxClients { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? GymName { get; set; }

    [MaxLength(500)]
    public string? GymAddress { get; set; }
}
