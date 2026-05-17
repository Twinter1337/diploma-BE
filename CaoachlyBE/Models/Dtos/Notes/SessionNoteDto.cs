namespace CaoachlyBE.Models.Dtos.Notes;

public class SessionNoteDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = null!;
    public bool IsPrivate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
