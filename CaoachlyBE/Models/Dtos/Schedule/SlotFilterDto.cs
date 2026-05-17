namespace CaoachlyBE.Models.Dtos.Schedule;

public class SlotFilterDto
{
    public bool? IsClosed { get; set; }
    public bool? IsReserved { get; set; }

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }

    public TimeOnly? TimeFrom { get; set; }
    public TimeOnly? TimeTo { get; set; }
}
