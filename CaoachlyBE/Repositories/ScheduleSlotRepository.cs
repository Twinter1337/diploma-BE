using CaoachlyBE.Entities;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class ScheduleSlotRepository(AppDbContext context) : IScheduleSlotRepository
{
    public async Task AddAsync(ScheduleSlotModel model)
    {
        var entity = new ScheduleSlot
        {
            Id = model.Id,
            TrainerId = model.TrainerId,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            Format = (short)model.Format,
            Price = model.Price,
            MaxClients = model.MaxClients,
            Status = (short)model.Status,
            CreatedAt = model.CreatedAt,
            Description = model.Description,
            GymAddress = model.GymAddress,
            GymName = model.GymName,
        };
        await context.ScheduleSlots.AddAsync(entity);
    }

    public async Task<ScheduleSlotModel?> GetByIdAsync(Guid id)
    {
        var entity = await context.ScheduleSlots.FirstOrDefaultAsync(s => s.Id == id);
        if (entity is null) return null;

        return new ScheduleSlotModel
        {
            Id = entity.Id,
            TrainerId = entity.TrainerId,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Format = (SlotFormat)entity.Format,
            Price = entity.Price,
            MaxClients = entity.MaxClients,
            Description = entity.Description,
            GymName = entity.GymName,
            GymAddress = entity.GymAddress,
            Status = (SlotStatus)entity.Status,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<IEnumerable<ScheduleSlotModel>> GetAvailableByTrainerIdAsync(Guid trainerId, bool isTrainer, SlotFilterDto filter)
    {
        var activeStatuses = new List<short> { (short)BookingStatus.Pending, (short)BookingStatus.Confirmed, (short)BookingStatus.Completed };

        var query = context.ScheduleSlots
            .Where(s => s.TrainerId == trainerId && (isTrainer || (s.Status == (short)SlotStatus.Available && s.StartTime > UaTime.Now)));

        var closedStatuses = new List<short> { (short)SlotStatus.Cancelled, (short)SlotStatus.Completed };
        var reservedStatuses = new List<short> { (short)SlotStatus.Booked, (short)SlotStatus.SoldOut };

        if (filter.IsClosed == true && filter.IsReserved == true)
            query = query.Where(s => closedStatuses.Contains(s.Status)
                && s.Bookings.Any(b => activeStatuses.Contains(b.Status)));
        else if (filter.IsClosed == true)
            query = query.Where(s => closedStatuses.Contains(s.Status));
        else if (filter.IsReserved == true)
            query = query.Where(s => reservedStatuses.Contains(s.Status));

        if (filter.MinPrice.HasValue)
            query = query.Where(s => s.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue)
            query = query.Where(s => s.Price <= filter.MaxPrice.Value);

        if (filter.DateFrom.HasValue)
            query = query.Where(s => DateOnly.FromDateTime(s.StartTime) >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(s => DateOnly.FromDateTime(s.StartTime) <= filter.DateTo.Value);

        if (filter.TimeFrom.HasValue)
            query = query.Where(s => TimeOnly.FromDateTime(s.StartTime) >= filter.TimeFrom.Value);
        if (filter.TimeTo.HasValue)
            query = query.Where(s => TimeOnly.FromDateTime(s.StartTime) <= filter.TimeTo.Value);

        var results = await query
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                Slot = s,
                CurrentNumOfClients = s.Bookings.Count(b => activeStatuses.Contains(b.Status))
            })
            .ToListAsync();

        return results.Select(r => new ScheduleSlotModel
        {
            Id = r.Slot.Id,
            TrainerId = r.Slot.TrainerId,
            StartTime = r.Slot.StartTime,
            EndTime = r.Slot.EndTime,
            Format = (SlotFormat)r.Slot.Format,
            Price = r.Slot.Price,
            MaxClients = r.Slot.MaxClients,
            Description = r.Slot.Description,
            GymName = r.Slot.GymName,
            GymAddress = r.Slot.GymAddress,
            Status = (SlotStatus)r.Slot.Status,
            CreatedAt = r.Slot.CreatedAt,
            CurrentNumOfClients = r.CurrentNumOfClients
        });
    }

    public async Task<(int total, int booked)> GetSlotCountByTrainerIdAsync(Guid trainerId)
    {
        var slots = await context.ScheduleSlots
            .Where(s => s.TrainerId == trainerId)
            .Select(s => s.Status)
            .ToListAsync();

        var total = slots.Count;
        var booked = slots.Count(s => s == (short)SlotStatus.Booked || s == (short)SlotStatus.SoldOut);
        return (total, booked);
    }

    public async Task<IEnumerable<Guid>> GetExpiredActiveAsync()
    {
        return await context.Database
            .SqlQueryRaw<Guid>("""
                SELECT id FROM schedule_slots
                WHERE end_time < now() AT TIME ZONE 'Europe/Kiev'
                  AND status <> 4
                  AND status <> 3
                """)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(Guid id, SlotStatus status)
    {
        var entity = await context.ScheduleSlots.FirstOrDefaultAsync(s => s.Id == id);
        if (entity is null) return;
        entity.Status = (short)status;
    }

    public async Task<bool> HasConflictAsync(Guid trainerId, DateTime startTime, DateTime endTime)
    {
        var excluded = new short[] { (short)SlotStatus.Cancelled, (short)SlotStatus.Completed };
        return await context.ScheduleSlots.AnyAsync(s =>
            s.TrainerId == trainerId &&
            !excluded.Contains(s.Status) &&
            s.StartTime < endTime &&
            s.EndTime > startTime);
    }

    public async Task UpdateAsync(Guid id, UpdateScheduleSlotDto dto)
    {
        var entity = await context.ScheduleSlots.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Slot not found.");

        if (dto.StartTime.HasValue) entity.StartTime = dto.StartTime.Value;
        if (dto.EndTime.HasValue) entity.EndTime = dto.EndTime.Value;
        if (dto.Format.HasValue) entity.Format = (short)dto.Format.Value;
        if (dto.Price.HasValue) entity.Price = dto.Price.Value;
        if (dto.MaxClients.HasValue) entity.MaxClients = dto.MaxClients.Value;
        if (dto.Description is not null) entity.Description = dto.Description;
        if (dto.GymName is not null) entity.GymName = dto.GymName;
        if (dto.GymAddress is not null) entity.GymAddress = dto.GymAddress;
    }
}
