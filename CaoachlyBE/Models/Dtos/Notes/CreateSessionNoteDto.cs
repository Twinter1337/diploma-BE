using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Notes;

public class CreateSessionNoteDto
{
    public Guid BookingId { get; set; }

    [Required]
    [MaxLength(5000)]
    public string Content { get; set; } = null!;

    public bool IsPrivate { get; set; }
}
