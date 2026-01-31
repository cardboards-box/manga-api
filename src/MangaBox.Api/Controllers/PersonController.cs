namespace MangaBox.Api.Controllers;

using static Constants;

public class PersonController(
    ILogger<PersonController> _logger,
    IDbService _db) : BaseController(_logger, _db)
{
    [HttpGet, Route("person/{id}")]
    [ProducesBox<Person>, ProducesError(404)]
    public Task<IActionResult> Get([FromRoute] Guid id) => Handle(async () =>
    {
        var person = await Database.People.Fetch(id);
        if (person is null) 
            return Boxed.NotFound("Person doesn't exist.");
        
        return Boxed.Ok(person);
    });

    [HttpGet, Route("person"), ProducesPaged<Person>]
    public Task<IActionResult> List(
        [FromQuery] int page = DEFAULT_PAGE,
        [FromQuery] int size = DEFAULT_SIZE) => Handle(async () =>
    {
        if (!Validator
            .Between(size, "size", MIN_SIZE, MAX_SIZE)
            .GreaterThan(page, "page", MIN_PAGE)
            .Validate(out var res))
            return res;

        var persons = await Database.People.Paginate(page, size);
        return Boxed.Ok(persons);
    });
}
