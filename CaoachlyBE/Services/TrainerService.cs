using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos;
using CaoachlyBE.Models.Dtos.Bookings;
using CaoachlyBE.Models.Dtos.Documents;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Models.Dtos.Tags;
using CaoachlyBE.Models.Dtos.Trainers;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class TrainerService(
    IUserRepository userRepository,
    ITrainerInfoRepository trainerInfoRepository,
    IScheduleSlotRepository scheduleSlotRepository,
    IBookingRepository bookingRepository,
    IPaymentRepository paymentRepository,
    ITrainerDocumentRepository trainerDocumentRepository,
    ISupportTicketRepository supportTicketRepository,
    IBlobStorageService blobStorageService,
    IEmailService emailService,
    IUnitOfWork unitOfWork,
    ITimeProvider timeProvider) : ITrainerService
{
    private static readonly HashSet<string> AllowedMimeTypes = ["image/jpeg", "image/png", "application/pdf"];
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf"];
    private const string DocumentsContainer = "trainer-documents";
    private const int MaxFileSizeBytes = 10_485_760;
    private static readonly TimeSpan DocumentSasTtl = TimeSpan.FromMinutes(30);
    public async Task<PagedResultDto<TrainerListItemDto>> SearchAsync(
        TrainerSearchFilterDto filter, int page, int pageSize, string sortBy, string sortOrder)
    {
        var searchFilter = new TrainerSearchFilter
        {
            SpecializationTagIds = filter.SpecializationTagIds,
            City = filter.City,
            MinPrice = filter.MinPrice,
            MaxPrice = filter.MaxPrice,
            MinRating = filter.MinRating,
            Name = filter.Name,
            IsVerified = filter.IsVerified,
            IsAccess = filter.IsAccess,
            MethodologyTagIds = filter.MethodologyTagIds,
            DisabilityTagIds = filter.DisabilityTagIds
        };

        var trainers = (await userRepository.SearchTrainersAsync(searchFilter)).ToList();

        var sorted = (sortBy.ToLower(), sortOrder.ToLower()) switch
        {
            ("price", "asc")  => trainers.OrderBy(t => t.MinSlotPrice ?? decimal.MaxValue),
            ("price", _)      => trainers.OrderByDescending(t => t.MinSlotPrice ?? decimal.MinValue),
            ("rating", "asc") => trainers.OrderBy(t => t.Rating),
            _                 => (IOrderedEnumerable<TrainerSummaryModel>)trainers.OrderByDescending(t => t.Rating)
        };

        var totalCount = trainers.Count;
        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TrainerListItemDto
            {
                Id = t.Id,
                FirstName = t.FirstName,
                LastName = t.LastName,
                VerificationStatus = t.VerificationStatus,
                IsAccessible = t.IsAccessible,
                Rating = t.Rating,
                ReviewsCount = t.ReviewsCount,
                MinPrice = t.MinSlotPrice,
                SpecializationTags = t.SpecializationTags
                    .Select(tag => new TagListItemDto { Id = tag.Id, Name = tag.Name, Category = tag.Category, Description = tag.Description })
                    .ToList(),
                DisabilityTags = t.DisabilityTags
                    .Select(tag => new TagListItemDto { Id = tag.Id, Name = tag.Name, Category = tag.Category, Description = tag.Description })
                    .ToList(),
                MethodologyTags = t.MethodologyTags
                    .Select(tag => new TagListItemDto { Id = tag.Id, Name = tag.Name, Category = tag.Category, Description = tag.Description })
                    .ToList(),
                City = t.City,
                AvatarUrl = t.AvatarUrl
            })
            .ToList();

        return new PagedResultDto<TrainerListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TrainerProfileDto> UpdateProfileAsync(Guid trainerId, Guid requestingUserId, OnboardTrainerRequestDto dto)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        if (user.Id != requestingUserId)
            throw new UnauthorizedAccessException();

        var now = timeProvider.Now;

        short? genderValue = dto.Gender.HasValue ? (short)dto.Gender.Value : null;
        await userRepository.PatchAsync(trainerId, dto.FirstName, dto.LastName, dto.AvatarUrl, dto.City, null, genderValue, dto.BirthDate, now);
        await trainerInfoRepository.PatchAsync(trainerId, dto.Bio, dto.ExperienceYears);

        if (dto.SpecializationTagIds is not null)
            await userRepository.ReplaceTagsByCategoryAsync(trainerId, TagCategory.Specialization, dto.SpecializationTagIds);

        if (dto.MethodologyTagIds is not null)
            await userRepository.ReplaceTagsByCategoryAsync(trainerId, TagCategory.Methodology, dto.MethodologyTagIds);

        if (dto.HasAccess == false)
            await userRepository.ReplaceTagsByCategoryAsync(trainerId, TagCategory.Disability, []);
        else if (dto.HasAccess == true && dto.AccessTagIds is not null)
            await userRepository.ReplaceTagsByCategoryAsync(trainerId, TagCategory.Disability, dto.AccessTagIds);

        await unitOfWork.SaveChangesAsync();

        var updatedUser = await userRepository.GetByIdAsync(trainerId);
        var trainerInfo = await trainerInfoRepository.GetByUserIdAsync(trainerId);
        var specializationTags = await userRepository.GetTagsByCategoryAsync(trainerId, TagCategory.Specialization);
        var methodologyTags = await userRepository.GetTagsByCategoryAsync(trainerId, TagCategory.Methodology);
        var accessTags = await userRepository.GetTagsByCategoryAsync(trainerId, TagCategory.Disability);

        return new TrainerProfileDto
        {
            Id = updatedUser!.Id,
            Email = updatedUser.Email,
            FirstName = updatedUser.FirstName,
            LastName = updatedUser.LastName,
            AvatarUrl = updatedUser.AvatarUrl,
            City = updatedUser.City,
            Gender = updatedUser.Gender,
            BirthDate = updatedUser.BirthDate,
            Bio = trainerInfo?.Bio,
            ExperienceYears = trainerInfo?.ExperienceYears ?? 0,
            VerificationStatus = trainerInfo?.VerificationStatus ?? VerificationStatus.NotVerified,
            Rating = trainerInfo?.Rating ?? 0,
            ReviewsCount = trainerInfo?.ReviewsCount ?? 0,
            SpecializationTags = specializationTags.Select(t => new TagListItemDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description }).ToList(),
            MethodologyTags = methodologyTags.Select(t => new TagListItemDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description }).ToList(),
            AccessTags = accessTags.Select(t => new TagListItemDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description }).ToList()
        };
    }

    public async Task<UploadDocumentResponseDto> UploadDocumentAsync(Guid trainerId, Guid requestingUserId, UploadDocumentRequestDto dto)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        if (user.Id != requestingUserId)
            throw new UnauthorizedAccessException();

        var file = dto.File;

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File exceeds the 10 MB limit.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            throw new InvalidOperationException("Only JPG, PNG, and PDF files are allowed.");

        var blobName = $"{trainerId}/{Guid.NewGuid()}{extension}";
        string fileUrl;
        await using (var stream = file.OpenReadStream())
        {
            fileUrl = await blobStorageService.UploadAsync(stream, blobName, file.ContentType, DocumentsContainer);
        }

        var model = new TrainerDocumentModel
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            FileUrl = fileUrl,
            FileName = Path.GetFileName(file.FileName),
            FileSizeBytes = (int)file.Length,
            DocumentType = dto.DocumentType,
            Status = DocumentStatus.Pending,
            UploadedAt = timeProvider.Now,
        };

        await trainerDocumentRepository.AddAsync(model);

        var now = timeProvider.Now;
        await supportTicketRepository.AddAsync(new SupportTicketModel
        {
            Id = Guid.NewGuid(),
            CreatedBy = trainerId,
            Subject = "Документ на перевірку",
            Description = model.FileName,
            Status = TicketStatus.Open,
            RelatedDocumentId = model.Id,
            AssignedTo = null,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await unitOfWork.SaveChangesAsync();

        return new UploadDocumentResponseDto
        {
            Id = model.Id,
            FileName = model.FileName,
            FileSizeBytes = model.FileSizeBytes,
            DocumentType = model.DocumentType,
            FileUrl = blobStorageService.GetReadSasUrl(model.FileUrl, DocumentsContainer, DocumentSasTtl),
        };
    }

    public async Task<IEnumerable<TrainerDocumentDto>> GetDocumentsAsync(Guid trainerId, Guid requestingUserId)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        if (user.Id != requestingUserId)
            throw new UnauthorizedAccessException();

        var documents = await trainerDocumentRepository.GetByTrainerIdAsync(trainerId);

        return documents.Select(d => new TrainerDocumentDto
        {
            Id = d.Id,
            TrainerId = d.TrainerId,
            FileName = d.FileName,
            FileSizeBytes = d.FileSizeBytes,
            FileUrl = blobStorageService.GetReadSasUrl(d.FileUrl, DocumentsContainer, DocumentSasTtl),
            DocumentType = d.DocumentType,
            Status = d.Status,
            RejectionReason = d.RejectionReason,
            ReviewedBy = d.ReviewedBy,
            ReviewedAt = d.ReviewedAt,
            UploadedAt = d.UploadedAt,
        });
    }

    public async Task DeleteDocumentAsync(Guid trainerId, Guid documentId, Guid requestingUserId)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        if (user.Id != requestingUserId)
            throw new UnauthorizedAccessException();

        var document = await trainerDocumentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (document.TrainerId != trainerId)
            throw new UnauthorizedAccessException();

        await supportTicketRepository.DetachAndCloseByDocumentAsync(documentId, DateTime.UtcNow);
        await blobStorageService.DeleteAsync(document.FileUrl, DocumentsContainer);
        await trainerDocumentRepository.DeleteAsync(documentId);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<TrainerSlotListItemDto>> GetAvailableSlotsAsync(Guid trainerId, bool isTrainer, SlotFilterDto filter)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        var slots = await scheduleSlotRepository.GetAvailableByTrainerIdAsync(trainerId, isTrainer, filter);

        return slots.Select(s => new TrainerSlotListItemDto
        {
            Id = s.Id,
            StartDateTime = s.StartTime,
            DurationInMinutes = (int)(s.EndTime - s.StartTime).TotalMinutes,
            Format = s.Format,
            Price = s.Price,
            MaxClients = s.MaxClients,
            CurrentNumOfClients = s.CurrentNumOfClients,
            Description = s.Description,
            GymName = s.GymName,
            GymAddress = s.GymAddress,
            Status = s.Status,
        });
    }

    public async Task<TrainerSlotCountDto> GetSlotCountAsync(Guid trainerId)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        var (total, booked) = await scheduleSlotRepository.GetSlotCountByTrainerIdAsync(trainerId);

        return new TrainerSlotCountDto
        {
            NumOfAllSlots = total,
            NumOfBookedSlots = booked
        };
    }

    public async Task<IEnumerable<TrainerBookingListItemDto>> GetFutureBookingsAsync(Guid trainerId)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        var bookings = await bookingRepository.GetFutureByTrainerIdAsync(trainerId);

        return bookings.Select(b => new TrainerBookingListItemDto
        {
            Id = b.Id,
            ClientId = b.ClientId,
            ClientFullName = b.ClientFullName,
            ClientAvatarUrl = b.ClientAvatarUrl,
            StartDateTime = b.StartTime,
            DurationInMinutes = (int)(b.EndTime - b.StartTime).TotalMinutes,
            Format = b.Format,
            Status = b.Status
        });
    }

    public async Task<IEnumerable<TrainerClientListItemDto>> GetClientsAsync(Guid trainerId)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        var clients = await bookingRepository.GetClientsByTrainerIdAsync(trainerId);

        return clients.Select(c => new TrainerClientListItemDto
        {
            ClientId = c.ClientId,
            ClientFullName = c.ClientFullName,
            ClientAvatarUrl = c.ClientAvatarUrl,
            NumOfClasses = c.NumOfClasses,
            LastSlotDate = c.LastSlotDate,
            Bio = c.Bio,
            Tags = c.Tags.Select(t => new TagDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description }).ToList()
        });
    }

    public async Task<TrainerStatsDto> GetStatsAsync(Guid trainerId)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        var trainerInfo = await trainerInfoRepository.GetByUserIdAsync(trainerId);

        var now = timeProvider.Now;
        var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0);

        var completedCount = await bookingRepository.GetCompletedCountByTrainerIdAsync(trainerId);
        var activeClients = await bookingRepository.GetActiveClientCountByTrainerIdAsync(
            trainerId, now.AddMonths(-1), now.AddMonths(1));
        var monthlyData = (await bookingRepository.GetCompletedCountPerMonthByTrainerIdAsync(
            trainerId, yearStart, now)).ToDictionary(x => x.Month, x => x.Count);

        var completedPerMonth = Enumerable.Range(1, now.Month)
            .Select(m => new MonthlySlotCountDto
            {
                Month = m,
                NumOfCompletedSlots = monthlyData.GetValueOrDefault(m, 0)
            })
            .ToList();

        return new TrainerStatsDto
        {
            NumOfCompletedSlots = completedCount,
            AvgRating = trainerInfo?.Rating ?? 0,
            ActiveClientsThisMonth = activeClients,
            CompletedSlotsPerMonth = completedPerMonth
        };
    }

    public async Task<TrainerPublicProfileDto?> GetPublicProfileAsync(Guid trainerId)
    {
        var profile = await userRepository.GetTrainerPublicProfileAsync(trainerId);
        if (profile is null) return null;

        return new TrainerPublicProfileDto
        {
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            BirthDate = profile.BirthDate,
            Gender = profile.Gender,
            VerificationStatus = profile.VerificationStatus,
            IsAccessible = profile.IsAccessible,
            AvatarUrl = profile.AvatarUrl,
            ExperienceYears = profile.ExperienceYears,
            Bio = profile.Bio,
            Rating = profile.Rating,
            MinPrice = profile.MinPrice,
            City = profile.City,
            NumOfReviews = profile.NumOfReviews,
            SpecializationTags = profile.SpecializationTags
                .Select(t => new TagListItemDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description })
                .ToList(),
            NumOfCompletedClasses = profile.NumOfCompletedClasses,
            MethodologyTags = profile.MethodologyTags
                .Select(t => new TagListItemDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description })
                .ToList(),
            DisabilityTags = profile.DisabilityTags
                .Select(t => new TagListItemDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description })
                .ToList(),
            NumOfActiveClients = profile.NumOfActiveClients
        };
    }

    public async Task<ScheduleSlotDto> UpdateSlotAsync(Guid slotId, Guid requestingUserId, UpdateScheduleSlotDto dto)
    {
        var slot = await scheduleSlotRepository.GetByIdAsync(slotId)
            ?? throw new KeyNotFoundException("Slot not found.");

        if (slot.TrainerId != requestingUserId)
            throw new UnauthorizedAccessException();

        var changes = BuildSlotChanges(slot, dto);

        await scheduleSlotRepository.UpdateAsync(slotId, dto);
        await unitOfWork.SaveChangesAsync();

        var updated = (await scheduleSlotRepository.GetByIdAsync(slotId))!;

        if (changes.Count > 0)
        {
            var bookedClients = await bookingRepository.GetConfirmedWithClientBySlotIdAsync(slotId);
            foreach (var booking in bookedClients)
            {
                await emailService.SendSlotUpdateNotificationAsync(booking.ClientEmail, new SlotUpdateNotificationData
                {
                    ClientFirstName = booking.ClientFirstName,
                    TrainerFullName = booking.TrainerFullName,
                    Changes = changes
                });
            }
        }

        return new ScheduleSlotDto
        {
            Id = updated.Id,
            TrainerId = updated.TrainerId,
            StartTime = updated.StartTime,
            EndTime = updated.EndTime,
            Format = updated.Format,
            Price = updated.Price,
            MaxClients = updated.MaxClients,
            Status = updated.Status,
            Description = updated.Description,
            GymName = updated.GymName,
            GymAddress = updated.GymAddress,
            CreatedAt = updated.CreatedAt
        };
    }

    private static List<SlotFieldChange> BuildSlotChanges(ScheduleSlotModel before, UpdateScheduleSlotDto dto)
    {
        var changes = new List<SlotFieldChange>();

        if (dto.StartTime.HasValue && dto.StartTime.Value.DateTime != before.StartTime)
            changes.Add(new SlotFieldChange
            {
                Field = "Start time",
                Before = before.StartTime.ToString("dd MMM yyyy, HH:mm"),
                After = dto.StartTime.Value.DateTime.ToString("dd MMM yyyy, HH:mm")
            });

        if (dto.EndTime.HasValue && dto.EndTime.Value.DateTime != before.EndTime)
            changes.Add(new SlotFieldChange
            {
                Field = "End time",
                Before = before.EndTime.ToString("dd MMM yyyy, HH:mm"),
                After = dto.EndTime.Value.DateTime.ToString("dd MMM yyyy, HH:mm")
            });

        if (dto.Format.HasValue && dto.Format.Value != before.Format)
            changes.Add(new SlotFieldChange
            {
                Field = "Format",
                Before = before.Format.ToString(),
                After = dto.Format.Value.ToString()
            });

        if (dto.MaxClients.HasValue && dto.MaxClients.Value != before.MaxClients)
            changes.Add(new SlotFieldChange
            {
                Field = "Max clients",
                Before = before.MaxClients.ToString(),
                After = dto.MaxClients.Value.ToString()
            });

        if (dto.Description is not null && dto.Description != before.Description)
            changes.Add(new SlotFieldChange
            {
                Field = "Description",
                Before = before.Description ?? "—",
                After = dto.Description
            });

        if (dto.GymName is not null && dto.GymName != before.GymName)
            changes.Add(new SlotFieldChange
            {
                Field = "Gym name",
                Before = before.GymName ?? "—",
                After = dto.GymName
            });

        if (dto.GymAddress is not null && dto.GymAddress != before.GymAddress)
            changes.Add(new SlotFieldChange
            {
                Field = "Gym address",
                Before = before.GymAddress ?? "—",
                After = dto.GymAddress
            });

        return changes;
    }

    public async Task<ScheduleSlotDto> CreateSlotAsync(Guid trainerId, Guid requestingUserId, CreateScheduleSlotDto dto)
    {
        var user = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (user.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        if (user.Id != requestingUserId)
            throw new UnauthorizedAccessException();

        var startLocal = dto.StartTime.DateTime;
        var endLocal = dto.EndTime.DateTime;

        if ((endLocal - startLocal).TotalMinutes < 60)
            throw new ArgumentException("Slot duration must be at least 60 minutes.");

        if ((endLocal - startLocal).TotalHours > 6)
            throw new ArgumentException("Slot duration must not exceed 6 hours.");

        var now = timeProvider.Now;

        if (startLocal <= now)
            throw new ArgumentException("Slot start time must be in the future.");

        if (await scheduleSlotRepository.HasConflictAsync(trainerId, startLocal, endLocal))
            throw new InvalidOperationException("A slot already exists for this trainer at the specified date and time.");

        var model = new ScheduleSlotModel
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            StartTime = startLocal,
            EndTime = endLocal,
            Format = dto.Format,
            Price = dto.PricePerSession,
            MaxClients = dto.MaxClients,
            Status = SlotStatus.Available,
            CreatedAt = now,
            Description = dto.Description,
            GymAddress = dto.GymAddress,
            GymName = dto.GymName,
        };

        await scheduleSlotRepository.AddAsync(model);
        await unitOfWork.SaveChangesAsync();

        return new ScheduleSlotDto
        {
            Id = model.Id,
            TrainerId = model.TrainerId,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            Format = model.Format,
            Price = model.Price,
            MaxClients = model.MaxClients,
            Status = model.Status,
            Description = dto.Description,
            CreatedAt = model.CreatedAt,
            GymAddress = dto.GymAddress,
            GymName = dto.GymName,
        };
    }

    public async Task DeleteSlotAsync(Guid slotId, Guid requestingUserId)
    {
        var slot = await scheduleSlotRepository.GetByIdAsync(slotId)
            ?? throw new KeyNotFoundException("Slot not found.");

        if (slot.TrainerId != requestingUserId)
            throw new UnauthorizedAccessException();

        if (slot.Status == SlotStatus.Cancelled)
            throw new InvalidOperationException("Slot is already cancelled.");

        var activeBookings = await bookingRepository.GetActiveWithPaymentBySlotIdAsync(slotId);
        var now = timeProvider.Now;

        // Track actual refunded amounts from Stripe for email notifications
        var refundedAmounts = new Dictionary<Guid, decimal>();

        foreach (var booking in activeBookings)
        {
            if (booking.PaymentStatus == PaymentStatus.Paid && booking.PaymentTransactionId is not null)
            {
                // Omit Amount — let Stripe refund whatever was actually charged.
                // This avoids mismatches when payment.Amount diverges from the real charge
                // (e.g. a late-fee session was created but the user paid the original session).
                var refundOptions = new Stripe.RefundCreateOptions
                {
                    PaymentIntent = booking.PaymentTransactionId
                };
                var refundService = new Stripe.RefundService();
                var refund = await refundService.CreateAsync(refundOptions);
                refundedAmounts[booking.BookingId] = refund.Amount / 100m;
                await paymentRepository.UpdateRefundAsync(booking.BookingId, now);
            }

            await bookingRepository.CancelAsync(booking.BookingId, CancelledBy.Trainer, "Slot cancelled by trainer.");
        }

        await scheduleSlotRepository.UpdateStatusAsync(slotId, SlotStatus.Cancelled);
        await unitOfWork.SaveChangesAsync();

        foreach (var booking in activeBookings)
        {
            if (refundedAmounts.TryGetValue(booking.BookingId, out var refundedAmount))
            {
                _ = emailService.SendRefundNotificationAsync(booking.ClientEmail, new RefundNotificationData(
                    ClientFirstName: booking.ClientFirstName,
                    TrainerName: booking.TrainerFullName,
                    RefundAmount: refundedAmount,
                    Currency: booking.PaymentCurrency!,
                    RefundPercentage: 100,
                    SessionStartTime: booking.SlotStartTime,
                    SessionEndTime: booking.SlotEndTime,
                    CancelledAt: now
                ));
            }
            else
            {
                _ = emailService.SendSlotCancelledNotificationAsync(booking.ClientEmail, new SlotCancelledNotificationData(
                    ClientFirstName: booking.ClientFirstName,
                    TrainerFullName: booking.TrainerFullName,
                    SessionStartTime: booking.SlotStartTime,
                    SessionEndTime: booking.SlotEndTime,
                    CancelledAt: now
                ));
            }
        }
    }

    public async Task<PagedResultDto<TrainerBookingListItemDto>> GetClientBookingsAsync(
        Guid trainerId, Guid requestingUserId, Guid clientId, BookingStatus? status, int page, int pageSize)
    {
        if (requestingUserId != trainerId)
            throw new UnauthorizedAccessException();

        var trainer = await userRepository.GetByIdAsync(trainerId)
            ?? throw new KeyNotFoundException("Trainer not found.");

        if (trainer.Role != UserRole.Trainer)
            throw new KeyNotFoundException("Trainer not found.");

        var client = await userRepository.GetByIdAsync(clientId)
            ?? throw new KeyNotFoundException("Client not found.");

        var (items, totalCount) = await bookingRepository.GetByTrainerAndClientAsync(trainerId, clientId, status, page, pageSize);

        var dtos = items.Select(b => new TrainerBookingListItemDto
        {
            Id = b.Id,
            ClientId = b.ClientId,
            ClientFullName = b.ClientFullName,
            ClientAvatarUrl = b.ClientAvatarUrl,
            StartDateTime = b.StartTime,
            DurationInMinutes = (int)(b.EndTime - b.StartTime).TotalMinutes,
            Format = b.Format,
            Status = b.Status
        });

        return new PagedResultDto<TrainerBookingListItemDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Page = page,
            PageSize = pageSize
        };
    }
}
