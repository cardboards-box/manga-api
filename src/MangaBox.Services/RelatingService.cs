namespace MangaBox.Services;

/// <summary>
/// A service for relating manga together
/// </summary>
public interface IRelatingService
{
	/// <summary>
	/// Relates a manga to another manga
	/// </summary>
	/// <param name="manga">The manga to relate</param>
	/// <returns>The response</returns>
	Task Relate(MbManga[] manga);

	/// <summary>
	/// Relates a manga to another manga
	/// </summary>
	/// <param name="id">The primary manga to relate</param>
	/// <param name="ids">The manga to relate</param>
	/// <returns>The response</returns>
	Task<Boxed> Relate(Guid id, params Guid[] ids);

	/// <summary>
	/// Un-relates a manga from its related manga
	/// </summary>
	/// <param name="id">The primary manga to relate</param>
	/// <param name="ids">The manga to relate</param>
	/// <returns>The response</returns>
	Task<Boxed> Unrelate(Guid id, params Guid[] ids);
}

internal class RelatingService(
	IDbService _db) : IRelatingService
{
	public async Task Relate(MbManga[] manga)
	{
		var workId = manga.FirstOrDefault(t => t.WorkId.HasValue)?.WorkId
			?? await _db.Work.Insert(new());
		await _db.Work.LinkManga(workId, [..manga.Select(t => t.Id)]);
		await _db.Work.ClearOrphaned();
	}

	public async Task<Boxed> Relate(Guid id, params Guid[] ids)
	{
		var manga = await _db.Manga.Get([..ids, id]);
		if (manga.Length == 0)
			return Boxed.NotFound(nameof(MbManga), "One of the manga was not found.");

		await Relate(manga);

		var primary = await _db.Manga.FetchWithRelationships(id);
		if (primary is null)
			return Boxed.NotFound(nameof(MbManga), "The manga was not found.");

		return Boxed.Ok(primary);
	}

	public async Task<Boxed> Unrelate(Guid id, params Guid[] ids)
	{
		var manga = await _db.Manga.Get(ids);
		if (manga.Length == 0)
			return Boxed.NotFound(nameof(MbManga), "One of the manga was not found.");

		foreach(var m in manga)
			if (m.WorkId is not null)
				await _db.Work.UnlinkManga(m.Id);

		await _db.Work.ClearOrphaned();

		var primary = await _db.Manga.FetchWithRelationships(id);
		if (primary is null)
			return Boxed.NotFound(nameof(MbManga), "The manga was not found.");

		return Boxed.Ok(primary);
	}
}
