namespace CaoachlyBE.Models;

public record ReceiptData(
    string ClientName,
    string TrainerName,
    decimal Amount,
    string Currency,
    DateTime SessionStartTime,
    DateTime SessionEndTime,
    string SessionFormat,
    string PaymentIntentId,
    DateTime PaidAt
);
