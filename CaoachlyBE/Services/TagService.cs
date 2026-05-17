using System.Text.Json;
using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tags;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace CaoachlyBE.Services;

public class TagService(
    ITagRepository tagRepository,
    IMapper mapper,
    IDistributedCache cache,
    IConfiguration configuration) : ITagService
{
    public async Task<IEnumerable<TagListItemDto>> GetByCategoryAsync(TagCategory category)
    {
        var key = $"tags:category:{(int)category}";

        try
        {
            var cached = await cache.GetStringAsync(key);
            if (cached is not null)
                return JsonSerializer.Deserialize<IEnumerable<TagListItemDto>>(cached)!;
        }
        catch (Exception)
        {
            // Redis unavailable — fall through to DB
        }

        var tags = await tagRepository.GetByCategoryAsync(category);
        var result = mapper.Map<IEnumerable<TagListItemDto>>(tags);

        try
        {
            var ttl = configuration.GetValue<int>("Cache:TagsTtlSeconds", 3600);
            await cache.SetStringAsync(key, JsonSerializer.Serialize(result),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl)
                });
        }
        catch (Exception)
        {
            // Redis unavailable — result still returned from DB
        }

        return result;
    }
}
