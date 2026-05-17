using System.ComponentModel.DataAnnotations;
using CaoachlyBE.Enums;

namespace CaoachlyBE.Models.Dtos.Documents;

public class ReviewDocumentDto
{
    public DocumentStatus Status { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }
}
