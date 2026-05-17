using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Auth;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;
}
