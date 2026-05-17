using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Auth;

public class LoginDto
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = null!;
}
