using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class SlotActiveBookingModel
{
    public Guid BookingId { get; set; }
    public BookingStatus Status { get; set; }
    public Guid ClientId { get; set; }
    public string ClientEmail { get; set; } = null!;
    public string ClientFirstName { get; set; } = null!;
    public string TrainerFullName { get; set; } = null!;
    public DateTime SlotStartTime { get; set; }
    public DateTime SlotEndTime { get; set; }
    public string? PaymentTransactionId { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public decimal? PaymentAmount { get; set; }
    public string? PaymentCurrency { get; set; }
}
