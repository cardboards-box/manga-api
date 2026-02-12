namespace MangaBox.Api.Controllers;

using Services.CBZModels;

/// <summary>
/// The controller for metadata endpoints
/// </summary>
public class MetaDataController(
	IDbService _db,
	IStatsService _stats,
	ILogger<MetaDataController> logger) : BaseController(logger)
{
	/// <summary>
	/// Gets the stats snapshot of the application
	/// </summary>
	/// <returns>The stats snapshots</returns>
	[HttpGet, Route("metadata/stats")]
	[ProducesArray<StatsItem>]
	public Task<IActionResult> GetStats() => Box(() =>
	{
		return Boxed.Ok(_stats.Snapshot);
	});

	/// <summary>
	/// Gets the metadata for the <see cref="ContentRating"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/content-rating")]
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
	[HttpGet, Route("metadata/relationship-type")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetRelationshipTypes() => Box(() =>
	{
		var values = RelationshipType.Author.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the metadata for the <see cref="ChapterOrderBy"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/chapter-order-by")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetChapterOrderBys() => Box(() =>
	{
		var values = ChapterOrderBy.Date.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the metadata for the <see cref="VolumeState"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/volume-state")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetVolumeStates() => Box(() =>
	{
		var values = VolumeState.Completed.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the metadata for the <see cref="MangaOrderBy"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/manga-order-by")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetMangaOrders() => Box(() =>
	{
		var values = MangaOrderBy.MangaCreatedAt.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the metadata for the <see cref="MangaState"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/manga-state")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetMangaStates() => Box(() =>
	{
		var values = MangaState.Favorited.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the metadata for the <see cref="ComicFormat"/> enum
	/// </summary>
	/// <returns>All of the enum descriptions</returns>
	[HttpGet, Route("metadata/download-format")]
	[ProducesArray<EnumDescription>]
	public Task<IActionResult> GetDownloadFormats() => Box(() =>
	{
		var values = ComicFormat.Zip.Describe(false, false);
		return Boxed.Ok(values);
	});

	/// <summary>
	/// Gets the manga tags
	/// </summary>
	/// <returns>The manga tags</returns>
	[HttpGet, Route("metadata/manga-tag")]
	[ProducesArray<MbTag>]
	public Task<IActionResult> GetTags() => Box(async () =>
	{
		var tags = await _db.Tag.Get();
		return Boxed.Ok(tags);
	});

	/// <summary>
	/// Gets the manga sources
	/// </summary>
	/// <returns>The manga sources</returns>
	[HttpGet, Route("metadata/sources")]
	[ProducesArray<MbSource>]
	public Task<IActionResult> GetSources() => Box(async () =>
	{
		var sources = await _db.Source.Get();
		return Boxed.Ok(sources);
	});
}
