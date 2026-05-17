using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos;
using CaoachlyBE.Models.Dtos.Bookings;
using CaoachlyBE.Models.Dtos.Documents;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Models.Dtos.Trainers;

namespace CaoachlyBE.Services.Interfaces;

public interface ITrainerService
{
    Task<PagedResultDto<TrainerListItemDto>> SearchAsync(TrainerSearchFilterDto filter, int page, int pageSize, string sortBy, string sortOrder);
    Task<TrainerProfileDto> UpdateProfileAsync(Guid trainerId, Guid requestingUserId, OnboardTrainerRequestDto dto);
    Task<UploadDocumentResponseDto> UploadDocumentAsync(Guid trainerId, Guid requestingUserId, UploadDocumentRequestDto dto);
    Task<IEnumerable<TrainerDocumentDto>> GetDocumentsAsync(Guid trainerId, Guid requestingUserId);
    Task DeleteDocumentAsync(Guid trainerId, Guid documentId, Guid requestingUserId);
    Task<ScheduleSlotDto> CreateSlotAsync(Guid trainerId, Guid requestingUserId, CreateScheduleSlotDto dto);
    Task<ScheduleSlotDto> UpdateSlotAsync(Guid slotId, Guid requestingUserId, UpdateScheduleSlotDto dto);
    Task<IEnumerable<TrainerSlotListItemDto>> GetAvailableSlotsAsync(Guid trainerId, bool isTrainer, SlotFilterDto filter);
    Task<TrainerSlotCountDto> GetSlotCountAsync(Guid trainerId);
    Task<IEnumerable<TrainerBookingListItemDto>> GetFutureBookingsAsync(Guid trainerId);
    Task<IEnumerable<TrainerClientListItemDto>> GetClientsAsync(Guid trainerId);
    Task<TrainerStatsDto> GetStatsAsync(Guid trainerId);
    Task<TrainerPublicProfileDto?> GetPublicProfileAsync(Guid trainerId);
    Task DeleteSlotAsync(Guid slotId, Guid requestingUserId);
    Task<PagedResultDto<TrainerBookingListItemDto>> GetClientBookingsAsync(Guid trainerId, Guid requestingUserId, Guid clientId, BookingStatus? status, int page, int pageSize);
}
