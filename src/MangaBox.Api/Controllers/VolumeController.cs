namespace MangaBox.Api.Controllers;

using static Constants;

public class VolumeController(
    ILogger<VolumeController> _logger,
    IDbService _db): BaseController(_logger, _db)
{
    [HttpGet, Route("volume"), ProducesPaged<Volume>]
    public Task<IActionResult> List(
        [FromQuery] int page = DEFAULT_PAGE,
        [FromQuery] int size = DEFAULT_SIZE) => Handle(async () =>
    {
        if (!Validator
            .Between(size, "size", MIN_SIZE, MAX_SIZE)
            .GreaterThan(page, "page", MIN_PAGE)
            .Validate(out var res))
        return res;

        var volumes = await Database.Volumes.Paginate(page, size);
        return Boxed.Ok(volumes);
    });

    [HttpGet, Route("volume/{id}")]
    [ProducesBox<Volume>, ProducesError(404)]
    public Task<IActionResult> Get([FromRoute] string id) => Handle(async () =>
    {
        if (!Validator
            .IsGuid(id, "id", out var guid)
            .Validate(out var res))
            return res;

        var volume = await Database.Volumes.Fetch(guid);
        if (volume is null) 
            return Boxed.NotFound("Volume");
        
        return Boxed.Ok(volume);
    });

    [HttpGet, Route("volume/{id}/series")]
    [ProducesArray<Volume>, ProducesError(404)]
    public Task<IActionResult> Series([FromRoute] string id) => Handle(async () =>
    {
        if (!Validator
            .IsGuid(id, "id", out var guid)
            .Validate(out var res))
            return res;

        var series = await Database.Volumes.BySeries(guid);
        return Boxed.Ok(series);
    });


    [HttpPost, Route("volume"), ProducesBox<Guid>, ProducesError(401)]
    public Task<IActionResult> Post([FromBody] Volume volume) => Bot(async (uid) =>
    {
        volume.Audit(uid);
        var id = await Database.Volumes.Upsert(volume);
        return Boxed.Ok(id);
    });
}
