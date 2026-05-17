using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Schedule;

public class TrainerSlotListItemDto
{
    public Guid Id { get; set; }
    public DateTime StartDateTime { get; set; }
    public int DurationInMinutes { get; set; }
    public SlotFormat Format { get; set; }
    public decimal Price { get; set; }
    public short MaxClients { get; set; }
    public int CurrentNumOfClients { get; set; }
    public string? Description { get; set; }
    public string? GymName { get; set; }
    public string? GymAddress { get; set; }
    public SlotStatus Status { get; set; }
}
