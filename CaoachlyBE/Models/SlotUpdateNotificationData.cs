namespace CaoachlyBE.Models;

public class SlotUpdateNotificationData
{
    public required string ClientFirstName { get; init; }
    public required string TrainerFullName { get; init; }
    public required List<SlotFieldChange> Changes { get; init; }
}

public class SlotFieldChange
{
    public required string Field { get; init; }
    public required string Before { get; init; }
    public required string After { get; init; }
}
