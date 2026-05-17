using CaoachlyBE.Models.Dtos.Stats;

namespace CaoachlyBE.Services.Interfaces;

public interface IStatsService
{
    Task<PlatformStatsDto> GetPlatformStatsAsync();
}
