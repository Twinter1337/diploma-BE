using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class TrainerDocument
{
    public Guid Id { get; set; }

    public Guid TrainerId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public int FileSizeBytes { get; set; }

    public short DocumentType { get; set; }

    public short Status { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User Trainer { get; set; } = null!;
}
