namespace MangaBox.Api.Controllers;

using Providers;

public class ProviderController(
    ILogger<ProviderController> _logger,
    IDbService _db,
    IImportService _importer): BaseController(_logger, _db)
{
    [HttpGet, Route("provider"), ProducesArray<Provider>]
    public Task<IActionResult> Get() => Handle(async () =>
    {
        var providers = await Database.Providers.Get();
        return Boxed.Ok(providers);
    });

    [HttpGet, Route("provider/load")]
    [ProducesBox<Guid>, ProducesError(404)]
    public Task<IActionResult> Load([FromQuery] string url) => Handle(() =>
    {
        return _importer.Load(url);
    });
}
