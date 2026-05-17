using System.ComponentModel.DataAnnotations;
using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Users;

public class UpdateUserDto
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
}
