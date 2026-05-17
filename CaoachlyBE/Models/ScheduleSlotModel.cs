using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class ScheduleSlotModel
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SlotFormat Format { get; set; }
    public decimal Price { get; set; }
    public short MaxClients { get; set; }
    public string? Description { get; set; }
    public string? GymName { get; set; }
    public string? GymAddress { get; set; }
    public SlotStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CurrentNumOfClients { get; set; }
}
