using CaoachlyBE.Enums;
using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface IBookingRepository
{
    Task<BookingModel?> GetByIdAsync(Guid id);
    Task<BookingModel?> GetByStripeSessionIdAsync(string sessionId);
    Task AddAsync(BookingModel model);
    Task<bool> HasConflictAsync(Guid clientId, DateTime start, DateTime end);
    Task UpdateStatusAsync(Guid id, BookingStatus status);
    Task CancelAsync(Guid id, CancelledBy cancelledBy, string? reason);
    Task<IEnumerable<ClientBookingModel>> GetByClientIdAsync(Guid clientId);
    Task<IEnumerable<BookingHistoryItemModel>> GetHistoryByClientIdAsync(Guid clientId);
    Task<IEnumerable<BookingModel>> GetPendingOrConfirmedByClientIdAsync(Guid clientId);
    Task<IEnumerable<BookingCompletionModel>> GetConfirmedWithClientBySlotIdAsync(Guid slotId);
    Task<IEnumerable<TrainerBookingListItemModel>> GetFutureByTrainerIdAsync(Guid trainerId);
    Task<IEnumerable<TrainerClientListItemModel>> GetClientsByTrainerIdAsync(Guid trainerId);
    Task<int> GetCompletedCountByTrainerIdAsync(Guid trainerId);
    Task<int> GetActiveClientCountByTrainerIdAsync(Guid trainerId, DateTime start, DateTime end);
    Task<IEnumerable<(int Month, int Count)>> GetCompletedCountPerMonthByTrainerIdAsync(Guid trainerId, DateTime start, DateTime end);
    Task<IEnumerable<(Guid BookingId, string StripeSessionId)>> GetExpiredPendingAsync(DateTime cutoff);
    Task<IEnumerable<SessionReminderInfo>> GetDueForReminderAsync();
    Task<IEnumerable<SlotActiveBookingModel>> GetActiveWithPaymentBySlotIdAsync(Guid slotId);
    Task<(IEnumerable<TrainerBookingListItemModel> Items, int TotalCount)> GetByTrainerAndClientAsync(Guid trainerId, Guid clientId, BookingStatus? status, int page, int pageSize);
}
