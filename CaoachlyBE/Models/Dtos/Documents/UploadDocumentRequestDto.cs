using System.ComponentModel.DataAnnotations;
using CaoachlyBE.Enums;
using Microsoft.AspNetCore.Http;

namespace CaoachlyBE.Models.Dtos.Documents;

public class UploadDocumentRequestDto
{
    [Required]
    public IFormFile File { get; set; } = null!;

    public DocumentType DocumentType { get; set; }
}
