namespace CaoachlyBE.Models;

public class SessionReminderData
{
    public string RecipientFirstName { get; set; } = null!;
    public string TrainerFullName { get; set; } = null!;
    public string ClientFullName { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public bool IsTrainer { get; set; }
}
