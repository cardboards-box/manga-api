namespace MangaBox.Services;

/// <summary>
/// A service for keeping track of stats
/// </summary>
public interface IStatsService
{
	/// <summary>
	/// The snapshots of the stats
	/// </summary>
	StatsItem[] Snapshot { get; }

	/// <summary>
	/// Takes a snapshot of the current stats
	/// </summary>
	Task TakeSnapshot();
}

internal class StatsService(
	ISqlService _sql,
	IMangaPublishService _publish) : IStatsService
{
	private readonly CapacityCollection<StatsItem> _stats = new(30);

	public Task<DatabaseStats[]> DatabaseStats()
	{
		const string QUERY = @"WITH meta AS (
    SELECT
        '2026-01-01T00:00:00.000' as span,
        'All Time' as period
    UNION ALL
    SELECT
        CURRENT_TIMESTAMP - interval '1 hour' as span,
        '1 Hour' as period
    UNION ALL
    SELECT
        CURRENT_TIMESTAMP - interval '24 hours' as span,
        '24 Hours' as period
    UNION ALL
    SELECT
        CURRENT_TIMESTAMP - interval '7 days' as span,
        '1 Week' as period
    UNION ALL
    SELECT
        CURRENT_TIMESTAMP - interval '30 days' as span,
        '30 Days' as period
    UNION ALL
    SELECT
        CURRENT_TIMESTAMP - interval '365 days' as span,
        '1 Year' as period
)
SELECT
    period,
    (SELECT COUNT(*) as count FROM mb_manga WHERE deleted_at IS NULL AND created_at > span) as manga_count,
    (SELECT COUNT(*) as count FROM mb_chapters WHERE deleted_at IS NULL AND created_at > span) as chapter_count,
    (SELECT COUNT(*) as count FROM mb_images WHERE deleted_at IS NULL AND created_at > span) as image_count,
    (SELECT COUNT(*) as count FROM mb_sources WHERE deleted_at IS NULL AND created_at > span) as source_count,
    (SELECT COUNT(*) as count FROM mb_people WHERE deleted_at IS NULL AND created_at > span) as people_count
FROM meta";
		return _sql.Get<DatabaseStats>(QUERY);
	}

	public async Task<QueueStats> QueueStats()
	{
		var manga = await _publish.NewManga.Queue.Length();
		var chapters = await _publish.NewChapters.Queue.Length();
		var images = await _publish.NewImages.Queue.Length();
		return new QueueStats
		{
			Manga = (int)manga,
			Chapters = (int)chapters,
			Images = (int)images
		};
	}

	public async Task TakeSnapshot()
	{
		var database = await DatabaseStats();
		var queue = await QueueStats();
		var item = new StatsItem(DateTime.UtcNow, queue, database);
		_stats.Add(item);
	}

	public StatsItem[] Snapshot => [.._stats];
}

/// <summary>
/// The various statistics for the given period
/// </summary>
public class DatabaseStats
{
	/// <summary>
	/// The time period the statistics are for
	/// </summary>
	[JsonPropertyName("period")]
	public string Period { get; set; } = string.Empty;

	/// <summary>
	/// The number of new manga loaded during the period
	/// </summary>
	[JsonPropertyName("manga")]
	public int MangaCount { get; set; }

	/// <summary>
	/// The number of new chapters loaded during the period
	/// </summary>
	[JsonPropertyName("chapters")]
	public int ChapterCount { get; set; }

	/// <summary>
	/// The number of new images loaded during the period
	/// </summary>
	[JsonPropertyName("images")]
	public int ImageCount { get; set; }

	/// <summary>
	/// The number of new sources loaded during the period
	/// </summary>
	[JsonPropertyName("sources")]
	public int SourceCount { get; set; }

	/// <summary>
	/// The number of new authors loaded during the period
	/// </summary>
	[JsonPropertyName("people")]
	public int PeopleCount { get; set; }
}

/// <summary>
/// The queue stats
/// </summary>
public class QueueStats
{
	/// <summary>
	/// The number of new manga in the queue
	/// </summary>
	[JsonPropertyName("manga")]
	public int Manga { get; set; }

	/// <summary>
	/// The number of new chapters in the queue
	/// </summary>
	[JsonPropertyName("chapters")]
	public int Chapters { get; set; }

	/// <summary>
	/// The number of images in the queue
	/// </summary>
	[JsonPropertyName("images")]
	public int Images { get; set; }
}

/// <summary>
/// A snapshot of the current stats
/// </summary>
/// <param name="Timestamp">When the snapshot was taken</param>
/// <param name="Queue">The queue stats</param>
/// <param name="Database">The database stats</param>
public record class StatsItem(
	[property: JsonPropertyName("timestamp")] DateTime Timestamp,
	[property: JsonPropertyName("queue")] QueueStats Queue,
	[property: JsonPropertyName("database")] DatabaseStats[] Database);