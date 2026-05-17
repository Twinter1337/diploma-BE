using CaoachlyBE.Models.Dtos.Tags;

namespace CaoachlyBE.Models.Dtos.Trainers;

public class TrainerClientListItemDto
{
    public Guid ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientAvatarUrl { get; set; }
    public int NumOfClasses { get; set; }
    public DateTime? LastSlotDate { get; set; }
    public string? Bio { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}
