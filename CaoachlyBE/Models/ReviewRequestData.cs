namespace CaoachlyBE.Models;

public record ReviewRequestData(
    string ClientFirstName,
    string TrainerFullName,
    DateTime SessionStartTime,
    DateTime SessionEndTime,
    Guid BookingId
);
