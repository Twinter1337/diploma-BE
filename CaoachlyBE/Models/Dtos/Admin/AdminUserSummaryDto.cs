namespace CaoachlyBE.Models.Dtos.Admin;

public class AdminUserSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
}
