namespace CaoachlyBE.Models;

public class BookingCompletionModel
{
    public Guid BookingId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientEmail { get; set; } = null!;
    public string ClientFirstName { get; set; } = null!;
    public string TrainerFullName { get; set; } = null!;
    public DateTime SlotStartTime { get; set; }
    public DateTime SlotEndTime { get; set; }
}
