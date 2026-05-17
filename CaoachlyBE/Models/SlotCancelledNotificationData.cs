namespace CaoachlyBE.Models;

public record SlotCancelledNotificationData(
    string ClientFirstName,
    string TrainerFullName,
    DateTime SessionStartTime,
    DateTime SessionEndTime,
    DateTime CancelledAt
);
