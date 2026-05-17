using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Schedule;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IScheduleSlotRepository
{
    Task AddAsync(ScheduleSlotModel model);
    Task<ScheduleSlotModel?> GetByIdAsync(Guid id);
    Task<IEnumerable<ScheduleSlotModel>> GetAvailableByTrainerIdAsync(Guid trainerId, bool isTrainer, SlotFilterDto filter);
    Task<(int total, int booked)> GetSlotCountByTrainerIdAsync(Guid trainerId);
    Task<IEnumerable<Guid>> GetExpiredActiveAsync();
    Task UpdateStatusAsync(Guid id, SlotStatus status);
    Task UpdateAsync(Guid id, UpdateScheduleSlotDto dto);
    Task<bool> HasConflictAsync(Guid trainerId, DateTime startTime, DateTime endTime);
}
