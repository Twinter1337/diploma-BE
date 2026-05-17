using CaoachlyBE.Enums;
using CaoachlyBE.Models;

namespace CaoachlyBE.Repositories.Interfaces;

public interface ITagRepository
{
    Task<IEnumerable<TagModel>> GetByCategoryAsync(TagCategory category);
}
