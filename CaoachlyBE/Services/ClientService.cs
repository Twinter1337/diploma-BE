using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models.Dtos.Clients;
using CaoachlyBE.Models.Dtos.Tags;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class ClientService(
    IUserRepository userRepository,
    IClientInfoRepository clientInfoRepository,
    IUnitOfWork unitOfWork) : IClientService
{
    public async Task<ClientProfileDto> UpdateProfileAsync(Guid clientId, Guid requestingUserId, OnboardClientRequestDto dto)
    {
        var user = await userRepository.GetByIdAsync(clientId)
            ?? throw new KeyNotFoundException("Client not found.");

        if (user.Role != UserRole.Client)
            throw new KeyNotFoundException("Client not found.");

        if (user.Id != requestingUserId)
            throw new UnauthorizedAccessException();

        var now = UaTime.Now;

        short? genderValue = dto.Gender.HasValue ? (short)dto.Gender.Value : null;
        await userRepository.PatchAsync(clientId, null, null, dto.AvatarUrl, dto.City, null, genderValue, dto.BirthDate, now);

        if (dto.FirstName is not null || dto.LastName is not null || dto.Email is not null)
            await userRepository.PatchIdentityAsync(clientId, dto.FirstName, dto.LastName, dto.Email, now);

        await clientInfoRepository.PatchAsync(clientId, dto.HeightCm, dto.WeightKg, dto.About);

        if (dto.AccessTagIds is not null)
            await userRepository.ReplaceTagsByCategoryAsync(clientId, TagCategory.Disability, dto.AccessTagIds);

        await unitOfWork.SaveChangesAsync();

        var updatedUser = await userRepository.GetByIdAsync(clientId);
        var clientInfo = await clientInfoRepository.GetByUserIdAsync(clientId);
        var accessTags = await userRepository.GetTagsByCategoryAsync(clientId, TagCategory.Disability);

        return new ClientProfileDto
        {
            Id = updatedUser!.Id,
            Email = updatedUser.Email,
            FirstName = updatedUser.FirstName,
            LastName = updatedUser.LastName,
            AvatarUrl = updatedUser.AvatarUrl,
            City = updatedUser.City,
            Gender = updatedUser.Gender,
            BirthDate = updatedUser.BirthDate,
            About = clientInfo?.FitnessGoals,
            HeightCm = clientInfo?.HeightCm,
            WeightKg = clientInfo?.WeightKg,
            AccessTags = accessTags.Select(t => new TagListItemDto { Id = t.Id, Name = t.Name, Category = t.Category, Description = t.Description }).ToList()
        };
    }
}
