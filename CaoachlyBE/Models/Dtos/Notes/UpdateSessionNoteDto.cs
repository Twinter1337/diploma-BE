using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Notes;

public class UpdateSessionNoteDto
{
    [MaxLength(5000)]
    public string? Content { get; set; }

    public bool? IsPrivate { get; set; }
}
