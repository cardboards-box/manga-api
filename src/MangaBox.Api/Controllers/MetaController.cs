namespace MangaBox.Api.Controllers;

public class MetaController(
    ILogger<MetaController> _logger,
    IDbService _db) : BaseController(_logger, _db)
{
    [HttpGet, Route("meta/roles"), ProducesArray<Role>]
    public Task<IActionResult> Roles() => Handle(async (_) =>
    {
        var roles = await Database.Roles.Get();
        return Boxed.Ok(roles);
    });

    [HttpGet, Route("meta/content-ratings"), ProducesArray<ContentRating>]
    public Task<IActionResult> ContentRatings() => Handle(async (_) =>
    {
        var ratings = await Database.ContentRatings.Get();
        return Boxed.Ok(ratings);
    });

    [HttpGet, Route("meta/tags"), ProducesArray<Tag>]
    public Task<IActionResult> Tags() => Handle(async (_) =>
    {
        var tags = await Database.Tags.Get();
        return Boxed.Ok(tags);
    });
}
