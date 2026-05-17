using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tags;

namespace CaoachlyBE.Services.Interfaces;

public interface ITagService
{
    Task<IEnumerable<TagListItemDto>> GetByCategoryAsync(TagCategory category);
}
