namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for metadata endpoints
/// </summary>
public class MetaDataController(
	IDbService _db,
	ILogger<MetaDataController> logger) : BaseController(logger)
{
	/// <summary>
	/// Gets the metadata for the <see cref="ContentRating"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/content-ratings")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetContentRatings() => Box(() =>
	{
		var values = ContentRating.Safe.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the metadata for the <see cref="RelationshipType"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/relationship-types")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetRelationshipTypes() => Box(() =>
	{
		var values = RelationshipType.Author.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the manga tags
	/// </summary>
	/// <returns>The manga tags</returns>
	[HttpGet, Route("metadata/manga-tags")]
	[ProducesArray<MbTag>]
	public Task<IActionResult> GetTags() => Box(async () =>
	{
		var tags = await _db.Tag.Get();
		return Boxed.Ok(tags);
	});
}
