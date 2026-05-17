using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Admin;

public class RejectDocumentRequestDto
{
    [MaxLength(500)]
    public string? RejectionReason { get; set; }
}
