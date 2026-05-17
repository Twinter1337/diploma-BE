namespace CaoachlyBE.Models;

public class ClientInfoModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public short? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? FitnessGoals { get; set; }
}
