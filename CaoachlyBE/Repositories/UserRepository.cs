using CaoachlyBE.Entities;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Stats;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public Task<bool> EmailExistsAsync(string email) =>
        context.Users.AnyAsync(u => u.Email == email);

    public async Task<UserModel?> GetByEmailAsync(string email)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (entity is null) return null;
        return new UserModel
        {
            Id = entity.Id,
            Email = entity.Email,
            PasswordHash = entity.PasswordHash,
            Role = (UserRole)entity.Role,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            AvatarUrl = entity.AvatarUrl,
            City = entity.City,
            Phone = entity.Phone,
            BirthDate = entity.BirthDate,
            Gender = entity.Gender.HasValue ? (Gender?)entity.Gender.Value : null,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task AddAsync(UserModel model)
    {
        var entity = new User
        {
            Id = model.Id,
            Email = model.Email,
            PasswordHash = model.PasswordHash,
            Role = (short)model.Role,
            FirstName = model.FirstName,
            LastName = model.LastName,
            AvatarUrl = model.AvatarUrl,
            City = model.City,
            Phone = model.Phone,
            BirthDate = model.BirthDate,
            Gender = model.Gender.HasValue ? (short?)model.Gender.Value : null,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
        await context.Users.AddAsync(entity);
    }

    public async Task<UserModel?> GetByIdAsync(Guid id)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (entity is null) return null;
        return new UserModel
        {
            Id = entity.Id,
            Email = entity.Email,
            PasswordHash = entity.PasswordHash,
            Role = (UserRole)entity.Role,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            AvatarUrl = entity.AvatarUrl,
            City = entity.City,
            Phone = entity.Phone,
            BirthDate = entity.BirthDate,
            Gender = entity.Gender.HasValue ? (Gender?)entity.Gender.Value : null,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task PatchAsync(Guid id, string? firstName, string? lastName, string? avatarUrl, string? city, string? phone, short? gender, DateOnly? birthDate, DateTime updatedAt)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (entity is null) return;
        if (firstName is not null) entity.FirstName = firstName;
        if (lastName is not null) entity.LastName = lastName;
        if (avatarUrl is not null) entity.AvatarUrl = avatarUrl;
        if (city is not null) entity.City = city;
        if (phone is not null) entity.Phone = phone;
        if (gender.HasValue) entity.Gender = gender.Value;
        if (birthDate.HasValue) entity.BirthDate = birthDate.Value;
        entity.UpdatedAt = updatedAt;
    }

    public async Task ClearAvatarUrlAsync(Guid userId, DateTime updatedAt)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (entity is null) return;
        entity.AvatarUrl = null;
        entity.UpdatedAt = updatedAt;
    }

    public async Task ReplaceTagsByCategoryAsync(Guid userId, TagCategory category, IEnumerable<int> tagIds)
    {
        var user = await context.Users
            .Include(u => u.Tags)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return;

        var tagsToRemove = user.Tags.Where(t => t.Category == (short)category).ToList();
        foreach (var tag in tagsToRemove)
            user.Tags.Remove(tag);

        var tagIdList = tagIds.ToList();
        if (tagIdList.Count > 0)
        {
            var newTags = await context.Tags
                .Where(t => tagIdList.Contains(t.Id) && t.Category == (short)category)
                .ToListAsync();
            foreach (var tag in newTags)
                user.Tags.Add(tag);
        }
    }

    public async Task<IEnumerable<TagModel>> GetTagsByCategoryAsync(Guid userId, TagCategory category)
    {
        var user = await context.Users
            .Include(u => u.Tags)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return [];

        return user.Tags
            .Where(t => t.Category == (short)category)
            .Select(t => new TagModel
            {
                Id = t.Id,
                Name = t.Name,
                Category = (TagCategory)t.Category,
                Description = t.Description
            })
            .ToList();
    }

    public async Task<IEnumerable<TrainerSummaryModel>> SearchTrainersAsync(TrainerSearchFilter filter)
    {
        var query = context.Users
            .Where(u => u.IsActive && u.Role == (short)UserRole.Trainer)
            .Include(u => u.TrainerInfo)
            .Include(u => u.Tags)
            .AsQueryable();

        if (filter.City is not null)
            query = query.Where(u => u.City == filter.City);

        if (filter.MinRating.HasValue)
            query = query.Where(u => u.TrainerInfo != null && u.TrainerInfo.Rating >= filter.MinRating.Value);

        if (filter.IsVerified == true)
            query = query.Where(u => u.TrainerInfo != null && u.TrainerInfo.VerificationStatus == (short)VerificationStatus.Verified);

        if (filter.IsAccess == true)
            query = query.Where(u => u.Tags.Any(t => t.Category == (short)TagCategory.Disability));

        if (filter.SpecializationTagIds is { Count: > 0 })
            query = query.Where(u => u.Tags.Any(t =>
                t.Category == (short)TagCategory.Specialization &&
                filter.SpecializationTagIds.Contains(t.Id)));

        if (filter.MethodologyTagIds is { Count: > 0 })
            query = query.Where(u => u.Tags.Any(t =>
                t.Category == (short)TagCategory.Methodology &&
                filter.MethodologyTagIds.Contains(t.Id)));

        if (filter.DisabilityTagIds is { Count: > 0 })
            query = query.Where(u => u.Tags.Any(t =>
                t.Category == (short)TagCategory.Disability &&
                filter.DisabilityTagIds.Contains(t.Id)));

        if (filter.MinPrice.HasValue)
            query = query.Where(u => u.ScheduleSlots.Any(s =>
                s.Status != (short)SlotStatus.Cancelled && s.Price >= filter.MinPrice.Value));

        if (filter.MaxPrice.HasValue)
            query = query.Where(u => u.ScheduleSlots.Any(s =>
                s.Status != (short)SlotStatus.Cancelled && s.Price <= filter.MaxPrice.Value));

        if (filter.Name is not null)
        {
            var parts = filter.Name.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                var word = parts[0];
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(word) ||
                    u.LastName.ToLower().Contains(word));
            }
            else
            {
                var p0 = parts[0];
                var p1 = parts[1];
                query = query.Where(u =>
                    (u.FirstName.ToLower().Contains(p0) && u.LastName.ToLower().Contains(p1)) ||
                    (u.FirstName.ToLower().Contains(p1) && u.LastName.ToLower().Contains(p0)));
            }
        }

        var entities = await query.ToListAsync();

        var trainerIds = entities.Select(u => u.Id).ToList();
        var now = UaTime.Now;
        var minPrices = await context.ScheduleSlots
            .Where(s => trainerIds.Contains(s.TrainerId) &&
                        s.Status == (short)SlotStatus.Available &&
                        s.StartTime > now)
            .GroupBy(s => s.TrainerId)
            .Select(g => new { TrainerId = g.Key, MinPrice = g.Min(s => s.Price) })
            .ToDictionaryAsync(x => x.TrainerId, x => x.MinPrice);

        var trainersWithoutAvailableSlots = trainerIds.Where(id => !minPrices.ContainsKey(id)).ToList();
        if (trainersWithoutAvailableSlots.Count > 0)
        {
            var fallbackPrices = await context.ScheduleSlots
                .Where(s => trainersWithoutAvailableSlots.Contains(s.TrainerId))
                .GroupBy(s => s.TrainerId)
                .Select(g => new { TrainerId = g.Key, Price = g.OrderByDescending(s => s.StartTime).First().Price })
                .ToDictionaryAsync(x => x.TrainerId, x => x.Price);
            foreach (var (id, price) in fallbackPrices)
                minPrices[id] = price;
        }

        return entities.Select(u => new TrainerSummaryModel
        {
            Id = u.Id,
            FirstName = u.FirstName,
            LastName = u.LastName,
            AvatarUrl = u.AvatarUrl,
            City = u.City,
            VerificationStatus = (VerificationStatus)(u.TrainerInfo?.VerificationStatus ?? 0),
            Rating = u.TrainerInfo?.Rating ?? 0,
            ReviewsCount = u.TrainerInfo?.ReviewsCount ?? 0,
            IsAccessible = u.Tags.Any(t => t.Category == (short)TagCategory.Disability),
            SpecializationTags = u.Tags
                .Where(t => t.Category == (short)TagCategory.Specialization)
                .Select(t => new TagModel { Id = t.Id, Name = t.Name, Category = TagCategory.Specialization, Description = t.Description })
                .ToList(),
            DisabilityTags = u.Tags
                .Where(t => t.Category == (short)TagCategory.Disability)
                .Select(t => new TagModel { Id = t.Id, Name = t.Name, Category = TagCategory.Disability, Description = t.Description })
                .ToList(),
            MethodologyTags = u.Tags
                .Where(t => t.Category == (short)TagCategory.Methodology)
                .Select(t => new TagModel { Id = t.Id, Name = t.Name, Category = TagCategory.Methodology, Description = t.Description })
                .ToList(),
            MinSlotPrice = minPrices.TryGetValue(u.Id, out var p) ? p : null
        }).ToList();
    }

    public async Task<TrainerPublicProfileModel?> GetTrainerPublicProfileAsync(Guid trainerId)
    {
        var user = await context.Users
            .Where(u => u.Id == trainerId && u.IsActive && u.Role == (short)UserRole.Trainer)
            .Include(u => u.TrainerInfo)
            .Include(u => u.Tags)
            .FirstOrDefaultAsync();

        if (user is null) return null;

        var now = UaTime.Now;
        var minPrice = await context.ScheduleSlots
            .Where(s => s.TrainerId == trainerId &&
                        s.Status == (short)SlotStatus.Available &&
                        s.StartTime > now)
            .MinAsync(s => (decimal?)s.Price)
            ?? await context.ScheduleSlots
                .Where(s => s.TrainerId == trainerId)
                .OrderByDescending(s => s.StartTime)
                .Select(s => (decimal?)s.Price)
                .FirstOrDefaultAsync();

        var numOfCompletedClasses = await context.Bookings
            .Where(b => b.Slot.TrainerId == trainerId && b.Status == (short)BookingStatus.Completed)
            .CountAsync();

        var lastWeek = UaTime.Now.AddDays(-7);
        var numOfActiveClients = await context.Bookings
            .Where(b => b.Slot.TrainerId == trainerId &&
                (b.Status == (short)BookingStatus.Confirmed ||
                 (b.Status == (short)BookingStatus.Completed && b.Slot.StartTime >= lastWeek)))
            .Select(b => b.ClientId)
            .Distinct()
            .CountAsync();

        return new TrainerPublicProfileModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            BirthDate = user.BirthDate,
            Gender = user.Gender.HasValue ? (Gender?)user.Gender.Value : null,
            VerificationStatus = (VerificationStatus)(user.TrainerInfo?.VerificationStatus ?? 0),
            IsAccessible = user.Tags.Any(t => t.Category == (short)TagCategory.Disability),
            AvatarUrl = user.AvatarUrl,
            ExperienceYears = user.TrainerInfo?.ExperienceYears ?? 0,
            Bio = user.TrainerInfo?.Bio,
            Rating = user.TrainerInfo?.Rating ?? 0,
            MinPrice = minPrice,
            City = user.City,
            NumOfReviews = user.TrainerInfo?.ReviewsCount ?? 0,
            SpecializationTags = user.Tags
                .Where(t => t.Category == (short)TagCategory.Specialization)
                .Select(t => new TagModel { Id = t.Id, Name = t.Name, Category = TagCategory.Specialization, Description = t.Description })
                .ToList(),
            NumOfCompletedClasses = numOfCompletedClasses,
            MethodologyTags = user.Tags
                .Where(t => t.Category == (short)TagCategory.Methodology)
                .Select(t => new TagModel { Id = t.Id, Name = t.Name, Category = TagCategory.Methodology, Description = t.Description })
                .ToList(),
            DisabilityTags = user.Tags
                .Where(t => t.Category == (short)TagCategory.Disability)
                .Select(t => new TagModel { Id = t.Id, Name = t.Name, Category = TagCategory.Disability, Description = t.Description })
                .ToList(),
            NumOfActiveClients = numOfActiveClients
        };
    }

    public async Task PatchIdentityAsync(Guid id, string? firstName, string? lastName, string? email, DateTime updatedAt)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (entity is null) return;
        if (firstName is not null) entity.FirstName = firstName;
        if (lastName is not null) entity.LastName = lastName;
        if (email is not null) entity.Email = email;
        entity.UpdatedAt = updatedAt;
    }

    public async Task UpdatePasswordHashAsync(Guid id, string passwordHash, DateTime updatedAt)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (entity is null) return;
        entity.PasswordHash = passwordHash;
        entity.UpdatedAt = updatedAt;
    }

    public async Task SoftDeleteAndAnonymizeAsync(Guid userId, DateTime updatedAt)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (entity is null) return;
        entity.IsActive = false;
        entity.FirstName = "Deleted";
        entity.LastName = "User";
        entity.Email = $"{userId}@deleted";
        entity.Phone = null;
        entity.AvatarUrl = null;
        entity.City = null;
        entity.UpdatedAt = updatedAt;
    }

    public async Task<PlatformStatsDto> GetPlatformStatsAsync()
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddMonths(-1);
        var windowEnd = now;

        var trainerIds = await context.Users
            .Where(u => u.IsActive && u.Role == (short)UserRole.Trainer)
            .Select(u => u.Id)
            .ToListAsync();

        var avgRating = await context.Reviews
            .Where(r => trainerIds.Contains(r.TrainerId)
                     && r.CreatedAt >= windowStart
                     && r.CreatedAt <= windowEnd)
            .AverageAsync(r => (decimal?)r.Rating) ?? 0m;

        var avgPrice = await context.ScheduleSlots
            .Where(s => trainerIds.Contains(s.TrainerId)
                     && s.Status != (short)SlotStatus.Cancelled
                     && s.StartTime >= windowStart
                     && s.StartTime <= windowEnd)
            .AverageAsync(s => (decimal?)s.Price) ?? 0m;

        var numOfCities = await context.ScheduleSlots
            .Where(s => trainerIds.Contains(s.TrainerId)
                     && s.Status != (short)SlotStatus.Cancelled
                     && s.StartTime >= windowStart
                     && s.StartTime <= windowEnd)
            .Join(context.Users, s => s.TrainerId, u => u.Id, (s, u) => u.City)
            .Where(city => city != null)
            .Distinct()
            .CountAsync();

        var numOfVerified = await context.TrainerInfos
            .Where(ti => trainerIds.Contains(ti.UserId) && ti.VerificationStatus == (short)VerificationStatus.Verified)
            .CountAsync();

        return new PlatformStatsDto
        {
            AvgRating = Math.Round(avgRating, 2),
            AvgPrice = Math.Round(avgPrice, 2),
            NumOfCities = numOfCities,
            NumOfVerified = numOfVerified,
            NumOfTrainers = trainerIds.Count
        };
    }

    public async Task<IEnumerable<UserModel>> GetByRoleAsync(UserRole role)
    {
        var entities = await context.Users.AsNoTracking()
            .Where(u => u.Role == (short)role && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync();

        return entities.Select(entity => new UserModel
        {
            Id = entity.Id,
            Email = entity.Email,
            PasswordHash = entity.PasswordHash,
            Role = (UserRole)entity.Role,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            AvatarUrl = entity.AvatarUrl,
            City = entity.City,
            Phone = entity.Phone,
            BirthDate = entity.BirthDate,
            Gender = entity.Gender.HasValue ? (Gender?)entity.Gender.Value : null,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        });
    }
}
