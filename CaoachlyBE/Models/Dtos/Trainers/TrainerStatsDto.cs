namespace CaoachlyBE.Models.Dtos.Trainers;

public class TrainerStatsDto
{
    public int NumOfCompletedSlots { get; set; }
    public decimal AvgRating { get; set; }
    public int ActiveClientsThisMonth { get; set; }
    public List<MonthlySlotCountDto> CompletedSlotsPerMonth { get; set; } = [];
}

public class MonthlySlotCountDto
{
    public int Month { get; set; }
    public int NumOfCompletedSlots { get; set; }
}
