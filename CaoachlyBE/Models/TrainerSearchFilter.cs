namespace CaoachlyBE.Models;

public class TrainerSearchFilter
{
    public List<int>? SpecializationTagIds { get; set; }
    public string? City { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinRating { get; set; }
    public string? Name { get; set; }
    public bool? IsVerified { get; set; }
    public bool? IsAccess { get; set; }
    public List<int>? MethodologyTagIds { get; set; }
    public List<int>? DisabilityTagIds { get; set; }
}
