using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Admin;

public class ReplyToTicketDto
{
    [Required, EmailAddress]
    public string SendTo { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;
}
