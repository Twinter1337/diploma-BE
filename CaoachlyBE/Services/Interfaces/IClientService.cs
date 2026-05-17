using CaoachlyBE.Models.Dtos.Clients;

namespace CaoachlyBE.Services.Interfaces;

public interface IClientService
{
    Task<ClientProfileDto> UpdateProfileAsync(Guid clientId, Guid requestingUserId, OnboardClientRequestDto dto);
}
