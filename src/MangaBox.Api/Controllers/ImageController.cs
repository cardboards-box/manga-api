namespace MangaBox.Api.Controllers;

using Caching;

[ResponseCache(Duration = 31536000)]
public class ImageController(
    ILogger<ImageController> _logger,
    IFileCacheService _cache,
    IDbService _db) : BaseController(_logger, _db)
{
    [NonAction]
    public async Task<IActionResult> FetchImage(Image? image, bool refresh)
    {
        if (image is null)
            return Do(Boxed.NotFound("Image", "Image ID not present in database"));

        var file = await _cache.Get(image, refresh);
        if (file is null)
            return Do(Boxed.NotFound("Image", "Image failed to fetch from CDN"));

        Response.Headers.Append("X-Image-Width", image.Width?.ToString());
        Response.Headers.Append("X-Image-Height", image.Height?.ToString());
        Response.Headers.Append("X-Image-Hash", image.Hash?.ToString());
        Response.Headers.Append("X-Image-Type", image.Type.ToString());

        return File(file.Stream, file.MimeType, file.FileName);
    }

    [HttpGet, Route("image/{id}")]
    public async Task<IActionResult> Image([FromRoute] string id, [FromQuery] bool refresh = false) 
    {
        if (!Validator
            .IsGuid(id, nameof(id), out var guid)
            .Validate(out var res))
            return Do(res);

        var image = await Database.Images.Fetch(guid);
        return await FetchImage(image, refresh);
    }

    [HttpGet, Route("image/cover/{seriesId}")]
    public async Task<IActionResult> Cover([FromRoute] string seriesId, [FromQuery] bool refresh = false)
    {
        if (!Validator
            .IsGuid(seriesId, nameof(seriesId), out var guid)
            .Validate(out var res))
            return Do(res);

        var image = await Database.Images.Cover(guid);
        return await FetchImage(image, refresh);
    }

    [HttpGet, Route("image/page/{pageId}")]
    public async Task<IActionResult> Page([FromRoute] string pageId, [FromQuery] bool refresh = false)
    {
        if (!Validator
            .IsGuid(pageId, nameof(pageId), out var guid)
            .Validate(out var res))
            return Do(res);

        var image = await Database.Images.Page(guid);
        return await FetchImage(image, refresh);
    }
}
