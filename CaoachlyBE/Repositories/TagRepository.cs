using CaoachlyBE.Enums;
using CaoachlyBE.Models;
using CaoachlyBE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaoachlyBE.Repositories;

public class TagRepository(AppDbContext context) : ITagRepository
{
    public async Task<IEnumerable<TagModel>> GetByCategoryAsync(TagCategory category)
    {
        return await context.Tags
            .Where(t => t.Category == (short)category)
            .Select(t => new TagModel
            {
                Id = t.Id,
                Name = t.Name,
                Category = (TagCategory)t.Category,
                Description = t.Description
            })
            .ToListAsync();
    }
}
