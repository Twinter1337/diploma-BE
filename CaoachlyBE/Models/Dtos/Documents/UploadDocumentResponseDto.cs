using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Documents;

public class UploadDocumentResponseDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public int FileSizeBytes { get; set; }
    public DocumentType DocumentType { get; set; }
    public string FileUrl { get; set; } = null!;
}
