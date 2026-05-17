namespace CaoachlyBE.Models;

public record RefundNotificationData(
    string ClientFirstName,
    string TrainerName,
    decimal RefundAmount,
    string Currency,
    int RefundPercentage,
    DateTime SessionStartTime,
    DateTime SessionEndTime,
    DateTime CancelledAt
);
