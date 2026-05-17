using CaoachlyBE.Models.Dtos.Reviews;

namespace CaoachlyBE.Services.Interfaces;

public interface IReviewService
{
    Task<IEnumerable<TrainerReviewDto>> GetByTrainerIdAsync(Guid trainerId);
    Task<CreateReviewResponseDto> CreateAsync(Guid clientId, CreateReviewDto dto);
}
