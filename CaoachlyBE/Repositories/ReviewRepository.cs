using CaoachlyBE.Entities;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class ReviewRepository(AppDbContext context) : IReviewRepository
{
    public async Task<IEnumerable<TrainerReviewModel>> GetByTrainerIdAsync(Guid trainerId)
    {
        var entities = await context.Reviews
            .Include(r => r.Client)
            .Where(r => r.TrainerId == trainerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return entities.Select(r => new TrainerReviewModel
        {
            AvatarUrl = r.Client.AvatarUrl,
            FullName = $"{r.Client.FirstName} {r.Client.LastName}",
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        });
    }

    public async Task AddAsync(ReviewModel model)
    {
        var entity = new Review
        {
            Id = model.Id,
            BookingId = model.BookingId,
            ClientId = model.ClientId,
            TrainerId = model.TrainerId,
            Rating = model.Rating,
            Comment = model.Comment,
            CreatedAt = model.CreatedAt
        };
        await context.Reviews.AddAsync(entity);
    }

    public Task<bool> ExistsByBookingIdAsync(Guid bookingId) =>
        context.Reviews.AnyAsync(r => r.BookingId == bookingId);
}
