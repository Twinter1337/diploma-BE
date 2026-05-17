using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Auth;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}
