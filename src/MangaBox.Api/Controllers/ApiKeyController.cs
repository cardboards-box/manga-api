namespace MangaBox.Api.Controllers;

using Jwt;

/// <summary>
/// A service for interacting with API key endpoints
/// </summary>
public class ApiKeyController(
    IDbService _db,
    IJwtKeyService _jwt,
    ILogger<ApiKeyController> logger) : BaseController(logger)
{
    /// <summary>
    /// Fetches the current profiles API keys
    /// </summary>
    /// <returns>The API keys</returns>
    [HttpGet, Route("api-key")]
    [ProducesArray<MbApiKey>, ProducesError(401)]
    public Task<IActionResult> Get() => Box(async () =>
    {
        var pid = this.GetProfileId();
        if (pid is null) return Boxed.Unauthorized();

        var keys = await _db.ApiKey.GetByProfile(pid.Value);
        return Boxed.Ok(keys);
    });

    /// <summary>
    /// Deletes the given API key
    /// </summary>
    /// <param name="id">The ID of the API key to delete</param>
    /// <returns>The result of the request</returns>
    [HttpDelete, Route("api-key/{id}")]
    [ProducesError(401), ProducesError(404)]
    public Task<IActionResult> Delete([FromRoute] string id) => Box(async () =>
    {
        if (!Guid.TryParse(id, out var guid))
            return Boxed.Bad("Invalid ID format.");

        var pid = this.GetProfileId();
        if (pid is null) return Boxed.Unauthorized();

        var key = await _db.ApiKey.FetchWithRelationships(guid);
        if (key is null) return Boxed.NotFound(nameof(MbApiKey));

        var profile = key.GetItem<MbProfile>();
        var canDelete = (profile is not null && profile.Id == pid.Value) || this.IsAdmin();
        if (!canDelete) return Boxed.NotFound(nameof(MbApiKey));

        await _db.ApiKey.Delete(guid);
        return Boxed.Ok();
    });

    /// <summary>
    /// Creates a new API key
    /// </summary>
    /// <param name="request">The request to create a new API key</param>
    /// <returns>The result of the request</returns>
    [HttpPost, Route("api-key")]
    [ProducesError(401), ProducesError(400)]
    public Task<IActionResult> Create([FromBody] ApiKeyCreate request) => Box(async () =>
    {
        var pid = this.GetProfileId();
        if (pid is null) return Boxed.Unauthorized();

        if (string.IsNullOrEmpty(request.Name))
            return Boxed.Bad("Name is required.");

        var key = new MbApiKey
        {
            ProfileId = pid.Value,
            Name = request.Name,
            Key = _jwt.GenerateKey(32)
        };
        if (!key.IsValid(out var errors))
            return Boxed.Bad(errors);

        var id = await _db.ApiKey.Insert(key);
        var created = await _db.ApiKey.Fetch(id);
        if (created is null) 
            return Boxed.Exception("Failed to create API key.");
        return Boxed.Ok(created);
    });

    /// <summary>
    /// Fetches the Key from the api-key
    /// </summary>
    /// <param name="id">The ID of the API key</param>
    /// <returns>The API key</returns>
    [HttpGet, Route("api-key/{id}/key")]
    [ProducesError(401), ProducesError(400)]
    public Task<IActionResult> Key([FromRoute] string id) => Box(async () =>
    {
        if (!Guid.TryParse(id, out var guid))
            return Boxed.Bad("Invalid ID format.");

        var pid = this.GetProfileId();
        if (pid is null) return Boxed.Unauthorized();

        var key = await _db.ApiKey.FetchWithRelationships(guid);
        if (key is null) return Boxed.NotFound(nameof(MbApiKey));

        var profile = key.GetItem<MbProfile>();
        var canView = (profile is not null && profile.Id == pid.Value) || this.IsAdmin();
        if (!canView) return Boxed.NotFound(nameof(MbApiKey));

        return Boxed.Ok(key.Entity.Key);
    });

    /// <summary>
    /// A request to create an API key
    /// </summary>
    /// <param name="Name">The name of the API key to create</param>
    public record class ApiKeyCreate(
        [property: JsonPropertyName("name")] string Name);
}