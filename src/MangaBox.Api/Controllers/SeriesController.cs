namespace MangaBox.Api.Controllers;

using static Constants;

public class SeriesController(
    ILogger<SeriesController> _logger,
    IDbService _db): BaseController(_logger, _db)
{
    [HttpGet, Route("series"), ProducesPaged<Series>]
    public Task<IActionResult> List(
        [FromQuery] int page = DEFAULT_PAGE,
        [FromQuery] int size = DEFAULT_SIZE) => Handle(async () =>
    {
        if (!Validator
            .Between(size, "size", MIN_SIZE, MAX_SIZE)
            .GreaterThan(page, "page", MIN_PAGE)
            .Validate(out var res))
        return res;

        var series = await Database.Series.Paginate(page, size);
        return Boxed.Ok(series);
    });

    [HttpGet, Route("series/{id}")]
    [ProducesBox<Series>, ProducesError(404)]
    public Task<IActionResult> Get([FromRoute] string id) => Handle(async () =>
    {
        if (!Validator
            .IsGuid(id, "id", out var guid)
            .Validate(out var res))
            return res;

        var series = await Database.Series.Fetch(guid);
        if (series is null) 
            return Boxed.NotFound("Series");
        
        return Boxed.Ok(series);
    });

    [HttpGet, Route("series/{id}/people")]
    [ProducesArray<PersonMap>, ProducesError(404)]
    public Task<IActionResult> People([FromRoute] string id) => Handle(async () =>
    {
        if (!Validator
            .IsGuid(id, "id", out var guid)
            .Validate(out var res))
            return res;

        var people = await Database.People.BySeries(guid);
        return Boxed.Ok(people);
    });

    [HttpPost, Route("series"), ProducesBox<Guid>, ProducesError(401)]
    public Task<IActionResult> Post([FromBody] Series series) => Bot(async (uid) =>
    {
        series.Audit(uid);
        var id = await Database.Series.Upsert(series);
        return Boxed.Ok(id);
    });
}
