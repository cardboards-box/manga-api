namespace MangaBox.Api.Controllers;

/// <summary>
/// A service for interacting with images
/// </summary>
public class ImageController(
	IDbService _db,
	IImageService _image,
	ILogger<ImageController> logger) : BaseController(logger)
{
	/// <summary>
	/// Gets the image metadata by it's ID
	/// </summary>
	/// <param name="id">The ID of the image</param>
	/// <returns>The image metadata or the error</returns>
	[HttpGet, Route("image/{id}/meta")]
	[Produces<MangaBoxType<MbImage>>, ProducesError(404), ProducesError(400)]
	public Task<IActionResult> ImageData([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var guid))
			return Boxed.Bad($"Invalid image ID: {id}");

		var result = await _db.Image.FetchWithRelationships(guid);
		if (result is null)
			return Boxed.NotFound($"Image not found: {id}");

		return Boxed.Ok(result);
	});

	/// <summary>
	/// Gets the image data by it's ID
	/// </summary>
	/// <param name="id">The ID of the image</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The image data or the error</returns>
	[HttpGet, Route("image/{id}")]
	[ProducesError(500), ProducesError(404), ProducesError(400)]
	[ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
	[ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
	public async Task<IActionResult> Get([FromRoute] string id, CancellationToken token)
	{
		if (!Guid.TryParse(id, out var guid))
			return await Box(() => Boxed.Bad($"Invalid image ID: {id}"));

		var result = await _image.Get(guid, token);
		if (!string.IsNullOrEmpty(result.Error) ||
			result.Stream is null)
			return await Box(() => Boxed.Exception(result.Error ?? "Image stream is missing"));

		if (result.Width.HasValue)
			Response.Headers.TryAdd("X-Image-Width", result.Width.Value.ToString());
		if (result.Height.HasValue)
			Response.Headers.TryAdd("X-Image-Height", result.Height.Value.ToString());
		Response.Headers.TryAdd("X-Image-Id", result.FileId.ToString());

		return File(result.Stream, result.MimeType ?? "application/octet-stream", result.FileName);
	}
}
