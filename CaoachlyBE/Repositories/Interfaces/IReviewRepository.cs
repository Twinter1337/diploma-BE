using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IReviewRepository
{
    Task<IEnumerable<TrainerReviewModel>> GetByTrainerIdAsync(Guid trainerId);
    Task AddAsync(ReviewModel model);
    Task<bool> ExistsByBookingIdAsync(Guid bookingId);
}
