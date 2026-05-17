using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models.Dtos.Tags;
using CaoachlyBE.Models.Dtos.Users;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CaoachlyBE.Services;

public class UserService(
    IUserRepository userRepository,
    IClientInfoRepository clientInfoRepository,
    ITagRepository tagRepository,
    IBookingRepository bookingRepository,
    IPaymentRepository paymentRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IBlobStorageService blobStorage) : IUserService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];
    private const long MaxFileSize = 10 * 1024 * 1024;

    public async Task<string> UploadAvatarAsync(Guid userId, Guid requestingUserId, IFormFile file)
    {
        if (requestingUserId != userId)
            throw new UnauthorizedAccessException();

        if (file.Length == 0)
            throw new ArgumentException("No file provided.");

        if (file.Length > MaxFileSize)
            throw new ArgumentException("File exceeds the 10 MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException("Only jpg and png files are allowed.");

        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(0, 8));
        if (read < 3 || !IsAllowedImageHeader(header))
            throw new ArgumentException("File content does not match an allowed image type.");

        stream.Seek(0, SeekOrigin.Begin);

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.AvatarUrl is not null)
            await blobStorage.DeleteAsync(user.AvatarUrl, "avatars");

        var blobName = $"{userId}/{Guid.NewGuid()}{ext}";
        var contentType = ext == ".png" ? "image/png" : "image/jpeg";
        var newUrl = await blobStorage.UploadAsync(stream, blobName, contentType, "avatars");

        await userRepository.PatchAsync(userId, null, null, newUrl, null, null, null, null, UaTime.Now);
        await unitOfWork.SaveChangesAsync();

        return newUrl;
    }

    public async Task<string?> GetAvatarUrlAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        return user?.AvatarUrl;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null) return null;

        var clientInfo = await clientInfoRepository.GetByUserIdAsync(userId);
        var allDisabilityTags = await tagRepository.GetByCategoryAsync(TagCategory.Disability);
        var userDisabilityTagIds = (await userRepository.GetTagsByCategoryAsync(userId, TagCategory.Disability))
            .Select(t => t.Id)
            .ToHashSet();

        return new UserProfileDto
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            City = user.City,
            HeightCm = clientInfo?.HeightCm,
            WeightKg = clientInfo?.WeightKg,
            AvatarUrl = user.AvatarUrl,
            DisabilityTags = allDisabilityTags.Select(t => new DisabilityTagItemDto
            {
                Id = t.Id,
                Name = t.Name,
                IsSelected = userDisabilityTagIds.Contains(t.Id)
            }).ToList()
        };
    }

    public async Task<UserProfileDto> UpdateUserAsync(Guid userId, Guid requestingUserId, UpdateUserDto dto)
    {
        if (requestingUserId != userId)
            throw new UnauthorizedAccessException();

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        await userRepository.PatchAsync(
            userId,
            dto.FirstName,
            dto.LastName,
            null,
            dto.City,
            dto.Phone,
            dto.Gender.HasValue ? (short?)dto.Gender.Value : null,
            dto.BirthDate,
            UaTime.Now);
        await unitOfWork.SaveChangesAsync();

        return (await GetUserProfileAsync(userId))!;
    }

    public async Task DeleteAvatarAsync(Guid userId, Guid requestingUserId)
    {
        if (requestingUserId != userId)
            throw new UnauthorizedAccessException();

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.AvatarUrl is null)
            throw new InvalidOperationException("No avatar to delete.");

        await blobStorage.DeleteAsync(user.AvatarUrl, "avatars");
        await userRepository.ClearAvatarUrlAsync(userId, UaTime.Now);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAccountAsync(Guid userId, Guid requestingUserId)
    {
        if (requestingUserId != userId)
            throw new UnauthorizedAccessException();

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var activeBookings = await bookingRepository.GetPendingOrConfirmedByClientIdAsync(userId);
        var now = UaTime.Now;

        foreach (var booking in activeBookings)
        {
            var payment = await paymentRepository.GetByBookingIdAsync(booking.Id);
            if (payment?.Status == PaymentStatus.Paid)
            {
                var refundOptions = new Stripe.RefundCreateOptions
                {
                    PaymentIntent = payment.TransactionId,
                    Amount = (long)(payment.Amount * 100)
                };
                var refundService = new Stripe.RefundService();
                await refundService.CreateAsync(refundOptions);
                await paymentRepository.UpdateRefundAsync(booking.Id, now);
            }

            await bookingRepository.CancelAsync(booking.Id, CancelledBy.Trainer, "Account deleted");
        }

        await refreshTokenRepository.InvalidateAllByUserIdAsync(userId);
        await userRepository.SoftDeleteAndAnonymizeAsync(userId, now);
        await unitOfWork.SaveChangesAsync();
    }

    private static bool IsAllowedImageHeader(byte[] header)
    {
        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return true;
        return false;
    }
}
