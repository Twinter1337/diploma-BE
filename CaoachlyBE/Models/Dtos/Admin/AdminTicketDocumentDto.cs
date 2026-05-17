namespace CaoachlyBE.Models.Dtos.Admin;

public class AdminTicketDocumentDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!; // "certificate" | "diploma" | "license" | "other"
    public string FileName { get; set; } = null!;
    public int FileSizeBytes { get; set; }
    public string FileUrl { get; set; } = null!;
    public short Status { get; set; } // 0=pending, 1=approved, 2=rejected
}
