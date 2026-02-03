namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// The service for interacting with the mb_chapter_progress table
/// </summary>
public interface IMbChapterProgressDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_chapter_progress table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbChapterProgress?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_chapter_progress table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbChapterProgress item);

    /// <summary>
    /// Updates a record in the mb_chapter_progress table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbChapterProgress item);

    /// <summary>
    /// Inserts a record in the mb_chapter_progress table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbChapterProgress item);

    /// <summary>
    /// Gets all of the records from the mb_chapter_progress table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbChapterProgress[]> Get();

	/// <summary>
	/// Updates the bookmarks for the given profile and chapter
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="chapterId">The ID of the chapter</param>
	/// <param name="bookmarks">The bookmarks to update</param>
	/// <returns>The updated chapter progress</returns>
	Task<MangaBoxType<MbMangaProgress>?> UpdateBookmarks(Guid profileId, Guid chapterId, int[] bookmarks);

	/// <summary>
	/// Updates the page ordinal for the given profile and chapter
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="chapterId">The ID of the chapter</param>
	/// <param name="pageOrdinal">The page ordinal to use</param>
	/// <returns>The updated chapter progress</returns>
	Task<MangaBoxType<MbMangaProgress>?> UpdateOrdinal(Guid profileId, Guid chapterId, int? pageOrdinal);
}

internal class MbChapterProgressDbService(
    IOrmService orm,
    IQueryCacheService _cache) : Orm<MbChapterProgress>(orm), IMbChapterProgressDbService
{
    public async Task<MangaBoxType<MbMangaProgress>?> GetProgress(string query, object? parameters = null)
    {
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(query, parameters);

        var progress = await rdr.ReadFirstOrDefaultAsync<MbMangaProgress>();
        if (progress is null) return null;

        var related = new List<MangaBoxRelationship>();
        var chapters = await rdr.ReadAsync<MbChapterProgress>();
        MangaBoxRelationship.Apply(related, chapters);

        return new(progress, [.. related]);
	}

	public async Task<MangaBoxType<MbMangaProgress>?> UpdateBookmarks(Guid profileId, Guid chapterId, int[] bookmarks)
	{
        var query = await _cache.Required("upsert_bookmarks");
        return await GetProgress(query, new
        {
            profileId,
            chapterId,
            bookmarks
        });
	}

    public async Task<MangaBoxType<MbMangaProgress>?> UpdateOrdinal(Guid profileId, Guid chapterId, int? pageOrdinal)
    {
        var query = await _cache.Required("upsert_chapter_progress");
        return await GetProgress(query, new
        {
            profileId,
            chapterId,
            pageOrdinal
        });
	}
}