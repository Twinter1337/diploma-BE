using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Reviews;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class ReviewService(
    IReviewRepository reviewRepository,
    IUserRepository userRepository,
    IBookingRepository bookingRepository,
    IScheduleSlotRepository slotRepository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IReviewService
{
    public async Task<IEnumerable<TrainerReviewDto>> GetByTrainerIdAsync(Guid trainerId)
    {
        var trainer = await userRepository.GetByIdAsync(trainerId);
        if (trainer is null)
            throw new KeyNotFoundException($"Trainer {trainerId} not found.");

        var models = await reviewRepository.GetByTrainerIdAsync(trainerId);
        return mapper.Map<IEnumerable<TrainerReviewDto>>(models);
    }

    public async Task<CreateReviewResponseDto> CreateAsync(Guid clientId, CreateReviewDto dto)
    {
        var booking = await bookingRepository.GetByIdAsync(dto.BookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.ClientId != clientId)
            throw new UnauthorizedAccessException();

        if (booking.Status != BookingStatus.Completed)
            throw new InvalidOperationException("Booking is not completed.");

        if (await reviewRepository.ExistsByBookingIdAsync(dto.BookingId))
            throw new InvalidOperationException("Review already exists for this booking.");

        var slot = await slotRepository.GetByIdAsync(booking.SlotId)
            ?? throw new KeyNotFoundException("Slot not found.");

        var review = new ReviewModel
        {
            Id = Guid.NewGuid(),
            BookingId = dto.BookingId,
            ClientId = clientId,
            TrainerId = slot.TrainerId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = UaTime.Now
        };

        await reviewRepository.AddAsync(review);
        await unitOfWork.SaveChangesAsync();

        return new CreateReviewResponseDto
        {
            Id = review.Id,
            BookingId = review.BookingId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        };
    }
}
