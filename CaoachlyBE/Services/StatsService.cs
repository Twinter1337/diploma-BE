using CaoachlyBE.Models.Dtos.Stats;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class StatsService(IUserRepository userRepository) : IStatsService
{
    public Task<PlatformStatsDto> GetPlatformStatsAsync() =>
        userRepository.GetPlatformStatsAsync();
}
