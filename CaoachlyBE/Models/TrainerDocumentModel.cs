using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

public class TrainerDocumentModel
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public string FileUrl { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public int FileSizeBytes { get; set; }
    public DocumentType DocumentType { get; set; }
    public DocumentStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UploadedAt { get; set; }
}
