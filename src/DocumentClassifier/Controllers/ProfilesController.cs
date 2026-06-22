using DocumentClassifier.Models;
using DocumentClassifier.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentClassifier.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileStore _store;

    public ProfilesController(IProfileStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<ClassificationProfile>> GetAll()
    {
        return Ok(_store.GetAll());
    }

    [HttpGet("{name}")]
    public ActionResult<ClassificationProfile> Get(string name)
    {
        var profile = _store.GetProfile(name);
        if (profile is null) return NotFound();
        return Ok(profile);
    }

    [HttpPut("{name}")]
    public ActionResult<ClassificationProfile> CreateOrUpdate(string name, [FromBody] ClassificationProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name != name)
            return BadRequest("Profile name must match route parameter.");

        if (profile.Categories is null || profile.Categories.Count == 0)
            return BadRequest("At least one category is required.");

        if (string.IsNullOrWhiteSpace(profile.SystemPrompt))
            return BadRequest("SystemPrompt is required.");

        _store.AddOrUpdate(profile);
        return Ok(profile);
    }

    [HttpDelete("{name}")]
    public ActionResult Delete(string name)
    {
        if (!_store.Delete(name))
            return NotFound();
        return NoContent();
    }
}
