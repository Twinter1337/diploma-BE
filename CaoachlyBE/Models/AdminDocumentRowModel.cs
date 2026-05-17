using CaoachlyBE.Enums;

namespace CaoachlyBE.Models;

/// <summary>Joined read-model used by admin queries on trainer_documents.</summary>
public class AdminDocumentRowModel
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public int FileSizeBytes { get; set; }
    public string FileUrl { get; set; } = null!;
    public DocumentType DocumentType { get; set; }
    public DocumentStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UploadedAt { get; set; }

    // Trainer (creator)
    public Guid TrainerId { get; set; }
    public string TrainerFirstName { get; set; } = null!;
    public string TrainerLastName { get; set; } = null!;
    public string TrainerEmail { get; set; } = null!;
    public string? TrainerAvatarUrl { get; set; }
}
