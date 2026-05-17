using CaoachlyBE.Enums;
using CaoachlyBE.Models.Dtos.Tags;
using CaoachlyBE.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CaoachlyBE.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController(ITagService tagService) : ControllerBase
{
    /// <summary>Returns tags filtered by category: 0=Specialization, 1=Disability, 2=Methodology.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TagListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByCategory([FromQuery] int? category)
    {
        if (category is null || category < 0 || category > 2)
            return BadRequest(new { message = "category is required and must be 0 (Specialization), 1 (Disability), or 2 (Methodology)." });

        var tags = await tagService.GetByCategoryAsync((TagCategory)(short)category.Value);
        return Ok(tags);
    }
}
