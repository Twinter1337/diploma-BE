namespace CaoachlyBE.Models;

public class SessionReminderInfo
{
    public Guid BookingId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientEmail { get; set; } = null!;
    public string ClientFirstName { get; set; } = null!;
    public string ClientLastName { get; set; } = null!;
    public Guid TrainerId { get; set; }
    public string TrainerEmail { get; set; } = null!;
    public string TrainerFirstName { get; set; } = null!;
    public string TrainerLastName { get; set; } = null!;
    public DateTime StartTime { get; set; }
}
