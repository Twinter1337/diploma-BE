using CaoachlyBE.Entities;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class BookingRepository(AppDbContext context) : IBookingRepository
{
    public async Task<BookingModel?> GetByIdAsync(Guid id)
    {
        var entity = await context.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<BookingModel?> GetByStripeSessionIdAsync(string sessionId)
    {
        var payment = await context.Payments
            .FirstOrDefaultAsync(p => p.TransactionId == sessionId);

        if (payment is null) return null;

        var entity = await context.Bookings
            .FirstOrDefaultAsync(b => b.Id == payment.BookingId);

        return entity is null ? null : MapToModel(entity);
    }

    public async Task AddAsync(BookingModel model)
    {
        var entity = new Booking
        {
            Id = model.Id,
            SlotId = model.SlotId,
            ClientId = model.ClientId,
            Status = (short)model.Status,
            CancellationReason = model.CancellationReason,
            CancelledBy = model.CancelledBy.HasValue ? (short?)model.CancelledBy.Value : null,
            ReminderMinutes = model.ReminderMinutes,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
        await context.Bookings.AddAsync(entity);
    }

    public async Task<bool> HasConflictAsync(Guid clientId, DateTime start, DateTime end)
    {
        return await context.Bookings
            .Where(b => b.ClientId == clientId && b.Status != (short)BookingStatus.Cancelled)
            .Join(context.ScheduleSlots,
                b => b.SlotId,
                s => s.Id,
                (b, s) => s)
            .AnyAsync(s => s.StartTime < end && s.EndTime > start);
    }

    public async Task UpdateStatusAsync(Guid id, BookingStatus status)
    {
        var entity = await context.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (entity is null) return;
        entity.Status = (short)status;
        entity.UpdatedAt = UaTime.Now;
    }

    public async Task CancelAsync(Guid id, CancelledBy cancelledBy, string? reason)
    {
        var entity = await context.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (entity is null) return;
        entity.Status = (short)BookingStatus.Cancelled;
        entity.CancelledBy = (short)cancelledBy;
        entity.CancellationReason = reason;
        entity.UpdatedAt = UaTime.Now;
    }

    public async Task<IEnumerable<BookingModel>> GetPendingOrConfirmedByClientIdAsync(Guid clientId)
    {
        var entities = await context.Bookings
            .Where(b => b.ClientId == clientId &&
                   (b.Status == (short)BookingStatus.Pending || b.Status == (short)BookingStatus.Confirmed))
            .ToListAsync();
        return entities.Select(MapToModel);
    }

    public async Task<IEnumerable<ClientBookingModel>> GetByClientIdAsync(Guid clientId)
    {
        return await context.Bookings
            .Where(b => b.ClientId == clientId
                     && b.Status != (short)BookingStatus.Cancelled
                     && b.Status != (short)BookingStatus.Completed)
            .Join(context.ScheduleSlots, b => b.SlotId, s => s.Id, (b, s) => new { b, s })
            .Join(context.Users, x => x.s.TrainerId, u => u.Id, (x, u) => new ClientBookingModel
            {
                Id = x.b.Id,
                Status = (BookingStatus)x.b.Status,
                TrainerFullName = $"{u.FirstName} {u.LastName}",
                TrainerAvatarUrl = u.AvatarUrl,
                StartTime = x.s.StartTime,
                EndTime = x.s.EndTime,
                Format = (SlotFormat)x.s.Format,
                Description = x.s.Description,
                GymName = x.s.GymName,
                GymAddress = x.s.GymAddress,
                CreatedAt = x.b.CreatedAt,
            })
            .Where(m => m.EndTime > UaTime.Now)
            .OrderBy(m => m.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<BookingHistoryItemModel>> GetHistoryByClientIdAsync(Guid clientId)
    {
        var query = from b in context.Bookings
                    join s in context.ScheduleSlots on b.SlotId equals s.Id
                    join u in context.Users on s.TrainerId equals u.Id
                    join r in context.Reviews on b.Id equals r.BookingId into reviews
                    from r in reviews.DefaultIfEmpty()
                    where b.ClientId == clientId
                        && b.Status != (short)BookingStatus.Cancelled
                        && s.EndTime < UaTime.Now
                    orderby s.StartTime descending
                    select new BookingHistoryItemModel
                    {
                        Id = b.Id,
                        StartTime = s.StartTime,
                        TrainerId = u.Id,
                        TrainerFullName = $"{u.FirstName} {u.LastName}",
                        TrainerAvatarUrl = u.AvatarUrl,
                        Price = s.Price,
                        Status = (BookingStatus)b.Status,
                        ReviewRating = r != null ? r.Rating : (short?)null,
                        ReviewComment = r != null ? r.Comment : null
                    };
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<BookingCompletionModel>> GetConfirmedWithClientBySlotIdAsync(Guid slotId)
    {
        return await (from b in context.Bookings
                      join s in context.ScheduleSlots on b.SlotId equals s.Id
                      join client in context.Users on b.ClientId equals client.Id
                      join trainer in context.Users on s.TrainerId equals trainer.Id
                      where b.SlotId == slotId && b.Status == (short)BookingStatus.Confirmed
                      select new BookingCompletionModel
                      {
                          BookingId = b.Id,
                          ClientId = b.ClientId,
                          ClientEmail = client.Email,
                          ClientFirstName = client.FirstName,
                          TrainerFullName = $"{trainer.FirstName} {trainer.LastName}",
                          SlotStartTime = s.StartTime,
                          SlotEndTime = s.EndTime
                      }).ToListAsync();
    }

    public async Task<IEnumerable<TrainerBookingListItemModel>> GetFutureByTrainerIdAsync(Guid trainerId)
    {
        return await (from b in context.Bookings
                      join s in context.ScheduleSlots on b.SlotId equals s.Id
                      join client in context.Users on b.ClientId equals client.Id
                      where s.TrainerId == trainerId
                          && s.StartTime > UaTime.Now
                          && (b.Status == (short)BookingStatus.Pending || b.Status == (short)BookingStatus.Confirmed)
                      orderby s.StartTime
                      select new TrainerBookingListItemModel
                      {
                          Id = b.Id,
                          ClientId = b.ClientId,
                          ClientFullName = $"{client.FirstName} {client.LastName}",
                          ClientAvatarUrl = client.AvatarUrl,
                          StartTime = s.StartTime,
                          EndTime = s.EndTime,
                          Format = (SlotFormat)s.Format,
                          Status = (BookingStatus)b.Status
                      }).ToListAsync();
    }

    public async Task<IEnumerable<TrainerClientListItemModel>> GetClientsByTrainerIdAsync(Guid trainerId)
    {
        var clients = await (from b in context.Bookings
                             join s in context.ScheduleSlots on b.SlotId equals s.Id
                             join client in context.Users on b.ClientId equals client.Id
                             where s.TrainerId == trainerId && b.Status != (short)BookingStatus.Cancelled
                             group new { b, s } by new { b.ClientId, client.FirstName, client.LastName, client.AvatarUrl } into g
                             select new TrainerClientListItemModel
                             {
                                 ClientId = g.Key.ClientId,
                                 ClientFullName = $"{g.Key.FirstName} {g.Key.LastName}",
                                 ClientAvatarUrl = g.Key.AvatarUrl,
                                 NumOfClasses = g.Count(),
                                 LastSlotDate = g.Where(x => x.b.Status == (short)BookingStatus.Completed)
                                                 .Select(x => (DateTime?)x.s.StartTime)
                                                 .Max()
                             })
                             .OrderByDescending(m => m.LastSlotDate)
                             .ToListAsync();

        var clientIds = clients.Select(c => c.ClientId).ToList();

        var bios = await context.ClientInfos
            .Where(ci => clientIds.Contains(ci.UserId))
            .Select(ci => new { ci.UserId, ci.FitnessGoals })
            .ToDictionaryAsync(ci => ci.UserId, ci => ci.FitnessGoals);

        var tagsByClient = await context.Users
            .Where(u => clientIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                Tags = u.Tags.Where(t => t.Category == (short)TagCategory.Disability).ToList()
            })
            .ToDictionaryAsync(x => x.Id, x => x.Tags);

        foreach (var client in clients)
        {
            client.Bio = bios.GetValueOrDefault(client.ClientId);
            client.Tags = tagsByClient.TryGetValue(client.ClientId, out var tags)
                ? tags.Select(t => new TagModel { Id = t.Id, Name = t.Name, Category = (TagCategory)t.Category, Description = t.Description }).ToList()
                : [];
        }

        return clients;
    }

    public async Task<int> GetCompletedCountByTrainerIdAsync(Guid trainerId)
    {
        return await context.Bookings
            .Where(b => b.Status == (short)BookingStatus.Completed)
            .Join(context.ScheduleSlots, b => b.SlotId, s => s.Id, (b, s) => s.TrainerId)
            .CountAsync(trainerId_ => trainerId_ == trainerId);
    }

    public async Task<int> GetActiveClientCountByTrainerIdAsync(Guid trainerId, DateTime start, DateTime end)
    {
        return await context.Bookings
            .Join(context.ScheduleSlots, b => b.SlotId, s => s.Id, (b, s) => new { b, s })
            .Where(x => x.s.TrainerId == trainerId
                && (x.b.Status == (short)BookingStatus.Pending || x.b.Status == (short)BookingStatus.Confirmed)
                && x.s.StartTime >= start && x.s.StartTime <= end)
            .Select(x => x.b.ClientId)
            .Distinct()
            .CountAsync();
    }

    public async Task<IEnumerable<(int Month, int Count)>> GetCompletedCountPerMonthByTrainerIdAsync(Guid trainerId, DateTime start, DateTime end)
    {
        var rows = await context.Bookings
            .Join(context.ScheduleSlots, b => b.SlotId, s => s.Id, (b, s) => new { b, s })
            .Where(x => x.s.TrainerId == trainerId
                && x.b.Status == (short)BookingStatus.Completed
                && x.s.StartTime >= start && x.s.StartTime <= end)
            .GroupBy(x => x.s.StartTime.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.Select(x => (x.Month, x.Count));
    }

    public async Task<IEnumerable<(Guid BookingId, string StripeSessionId)>> GetExpiredPendingAsync(DateTime cutoff)
    {
        var rows = await context.Bookings
            .Where(b => b.Status == (short)BookingStatus.Pending && b.CreatedAt < cutoff)
            .Join(context.Payments,
                b => b.Id,
                p => p.BookingId,
                (b, p) => new { b.Id, p.TransactionId, p.Status })
            .Where(x => x.Status == (short)PaymentStatus.Pending && x.TransactionId != null)
            .Select(x => new { x.Id, x.TransactionId })
            .ToListAsync();

        return rows.Select(x => (x.Id, x.TransactionId!));
    }

    public async Task<IEnumerable<SessionReminderInfo>> GetDueForReminderAsync()
    {
        var now = UaTime.Now;
        return await context.Database.SqlQuery<SessionReminderInfo>($"""
            SELECT
                b.id            AS "BookingId",
                b.client_id     AS "ClientId",
                c.email         AS "ClientEmail",
                c.first_name    AS "ClientFirstName",
                c.last_name     AS "ClientLastName",
                s.trainer_id    AS "TrainerId",
                t.email         AS "TrainerEmail",
                t.first_name    AS "TrainerFirstName",
                t.last_name     AS "TrainerLastName",
                s.start_time    AS "StartTime"
            FROM bookings b
            JOIN schedule_slots s ON b.slot_id = s.id
            JOIN users c ON b.client_id = c.id
            JOIN users t ON s.trainer_id = t.id
            WHERE b.status = 1
              AND EXTRACT(EPOCH FROM (s.start_time - {now})) / 60.0 <= b.reminder_minutes
              AND NOT EXISTS (
                SELECT 1 FROM notifications n
                WHERE n.booking_id = b.id
                  AND n.type = 6
              )
            """)
            .ToListAsync();
    }

    public async Task<IEnumerable<SlotActiveBookingModel>> GetActiveWithPaymentBySlotIdAsync(Guid slotId)
    {
        return await (from b in context.Bookings
                      join s in context.ScheduleSlots on b.SlotId equals s.Id
                      join client in context.Users on b.ClientId equals client.Id
                      join trainer in context.Users on s.TrainerId equals trainer.Id
                      join p in context.Payments on b.Id equals p.BookingId into payments
                      from p in payments.DefaultIfEmpty()
                      where b.SlotId == slotId
                          && (b.Status == (short)BookingStatus.Pending || b.Status == (short)BookingStatus.Confirmed)
                      select new SlotActiveBookingModel
                      {
                          BookingId = b.Id,
                          Status = (BookingStatus)b.Status,
                          ClientId = b.ClientId,
                          ClientEmail = client.Email,
                          ClientFirstName = client.FirstName,
                          TrainerFullName = $"{trainer.FirstName} {trainer.LastName}",
                          SlotStartTime = s.StartTime,
                          SlotEndTime = s.EndTime,
                          PaymentTransactionId = p != null ? p.TransactionId : null,
                          PaymentStatus = p != null ? (PaymentStatus?)(PaymentStatus)p.Status : null,
                          PaymentAmount = p != null ? (decimal?)p.Amount : null,
                          PaymentCurrency = p != null ? p.Currency : null
                      }).ToListAsync();
    }

    private static BookingModel MapToModel(Booking entity) => new()
    {
        Id = entity.Id,
        SlotId = entity.SlotId,
        ClientId = entity.ClientId,
        Status = (BookingStatus)entity.Status,
        CancellationReason = entity.CancellationReason,
        CancelledBy = entity.CancelledBy.HasValue ? (CancelledBy?)entity.CancelledBy.Value : null,
        ReminderMinutes = entity.ReminderMinutes,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public async Task<(IEnumerable<TrainerBookingListItemModel> Items, int TotalCount)> GetByTrainerAndClientAsync(
        Guid trainerId, Guid clientId, BookingStatus? status, int page, int pageSize)
    {
        var query = from b in context.Bookings
                    join s in context.ScheduleSlots on b.SlotId equals s.Id
                    join client in context.Users on b.ClientId equals client.Id
                    where s.TrainerId == trainerId && b.ClientId == clientId
                    select new { b, s, client };

        if (status.HasValue)
            query = query.Where(x => x.b.Status == (short)status.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.s.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TrainerBookingListItemModel
            {
                Id = x.b.Id,
                ClientId = x.b.ClientId,
                ClientFullName = $"{x.client.FirstName} {x.client.LastName}",
                ClientAvatarUrl = x.client.AvatarUrl,
                StartTime = x.s.StartTime,
                EndTime = x.s.EndTime,
                Format = (SlotFormat)x.s.Format,
                Status = (BookingStatus)x.b.Status
            })
            .ToListAsync();

        return (items, totalCount);
    }
}
