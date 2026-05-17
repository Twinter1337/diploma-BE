using CaoachlyBE.Models.Dtos.Users;
using Microsoft.AspNetCore.Http;

namespace CaoachlyBE.Services.Interfaces;

public interface IUserService
{
    Task<string> UploadAvatarAsync(Guid userId, Guid requestingUserId, IFormFile file);
    Task<string?> GetAvatarUrlAsync(Guid userId);
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<UserProfileDto> UpdateUserAsync(Guid userId, Guid requestingUserId, UpdateUserDto dto);
    Task DeleteAvatarAsync(Guid userId, Guid requestingUserId);
    Task DeleteAccountAsync(Guid userId, Guid requestingUserId);
}
