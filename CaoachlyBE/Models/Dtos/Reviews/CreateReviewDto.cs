using System.ComponentModel.DataAnnotations;

namespace CaoachlyBE.Models.Dtos.Reviews;

public class CreateReviewDto
{
    public Guid BookingId { get; set; }

    [Range(1, 5)]
    public short Rating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}
