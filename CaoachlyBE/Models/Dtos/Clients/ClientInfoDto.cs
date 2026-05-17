namespace CaoachlyBE.Models.Dtos.Clients;

public class ClientInfoDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public short? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? FitnessGoals { get; set; }
}
