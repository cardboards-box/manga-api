namespace MangaBox.Api.Controllers;

using static Constants;

public class UserController(
    IDbService _db,
    ILogger<UserController> _logger) : BaseController(_logger, _db)
{
    [HttpGet, Route("user/@me")]
    [ProducesBox<Profile>, ProducesError(401)]
    public Task<IActionResult> Me() => Authorized(async (pid) =>
    {
        var profile = await Database.Profiles.Fetch(pid);
        if (profile is null) 
            return Boxed.Unauthorized("Profile doesn't exist?");

        return Boxed.Ok(profile);
    });

    [HttpGet, Route("user/{id}")]
    [ProducesBox<PublicProfile>, ProducesError(404)]
    public Task<IActionResult> Get([FromRoute] Guid id) => Handle(async () =>
    {
        var profile = await Database.Profiles.Fetch(id);
        if (profile is null) 
            return Boxed.NotFound("Profile doesn't exist.");

        return Boxed.Ok(PublicProfile.From(profile));
    });

    [HttpGet, Route("user"), ProducesPaged<PublicProfile>]
    public Task<IActionResult> List(
        [FromQuery] int page = DEFAULT_PAGE, 
        [FromQuery] int size = DEFAULT_SIZE) => Handle(async () =>
    {
        if (!Validator
            .Between(size, "size", MIN_SIZE, MAX_SIZE)
            .GreaterThan(page, "page", MIN_PAGE)
            .Validate(out var res))
            return res;

        var profiles = await Database.Profiles.Paginate(page, size);
        return Boxed.Ok(PublicProfile.From(profiles));
    });
}
