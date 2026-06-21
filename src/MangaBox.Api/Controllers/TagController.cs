namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for tag endpoints
/// </summary>
public class TagController(
	IDbService _db,
	ILogger<TagController> logger) : BaseController(logger)
{
	/// <summary>
	/// Gets the manga tags
	/// </summary>
	/// <returns>The manga tags</returns>
	[HttpGet, Route("tag"), Route("metadata/manga-tag")]
	[ProducesArray<MangaBoxType<MbTag>>]
	public Task<IActionResult> Get() => Box(async () =>
	{
		var tags = await _db.Tag.GetWithRelationships();
		return Boxed.Ok(tags);
	});

	/// <summary>
	/// Updates the manga tags
	/// </summary>
	/// <param name="tags">The tags to update</param>
	/// <returns>The updated tags</returns>
	[HttpPost, Route("tag")]
	public Task<IActionResult> Post([FromBody] MbTag[] tags) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized();
		foreach (var tag in tags)
			tag.Id = await _db.Tag.Upsert(tag);
		return Boxed.Ok(tags);
	});

	/// <summary>
	/// Merges alias tags into a remaining tag
	/// </summary>
	/// <param name="id">The tag ID to keep</param>
	/// <param name="aliases">The tag IDs to merge into the kept tag</param>
	/// <returns>The updated tag and deleted tag IDs</returns>
	[HttpPost, Route("tag/merge/{id}")]
	public Task<IActionResult> Merge([FromRoute] Guid id, [FromBody] Guid[] aliases) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized();
		var result = await _db.Tag.MergeAliases(id, aliases);
		return Boxed.Ok(result);
	});
}
