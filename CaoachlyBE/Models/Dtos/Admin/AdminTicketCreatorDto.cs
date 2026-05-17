namespace CaoachlyBE.Models.Dtos.Admin;

public class AdminTicketCreatorDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!; // "Client" | "Trainer" | "Admin"
    public string Email { get; set; } = null!;
    public string? AvatarUrl { get; set; }
}
