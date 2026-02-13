namespace MangaBox.Services;

/// <summary>
/// A service for keeping track of stats
/// </summary>
public interface IStatsService
{
	/// <summary>
	/// The snapshots of the stats
	/// </summary>
	StatsItem Snapshot { get; }

	/// <summary>
	/// Takes a snapshot of the current stats
	/// </summary>
	Task TakeSnapshot();
}

internal class StatsService(
	ISqlService _sql,
	IMangaPublishService _publish) : IStatsService
{
	private readonly CapacityCollection<QueueStats> _queueStats = new(20);
	private DatabaseStats[]? _dbStats;

	public Task<DatabaseStats[]> DatabaseStats()
	{
		const string QUERY = @"WITH input AS (
    SELECT 
        '24 Hours' AS period, 
        'hour'::text AS trunc_unit,
        '1 hour'::interval AS bucket, 
        24::int AS n
    UNION ALL
    SELECT '1 Week',    'day'::text,   '1 day'::interval,   7::int UNION ALL
    SELECT '4 Weeks',   'week'::text,  '1 week'::interval,  4::int UNION ALL
    SELECT '12 Months', 'month'::text, '1 month'::interval, 12::int
), bounds AS (
    SELECT period, bucket, n, date_trunc(trunc_unit, now()) AS end_ts FROM input
), series AS (
    SELECT b.period, gs AS span_s, gs + b.bucket AS span_e FROM bounds b
    CROSS JOIN LATERAL generate_series(b.end_ts - (b.n - 1) * b.bucket, b.end_ts, b.bucket) AS gs
)
SELECT
    s.period, s.span_s AS period_start, s.span_e AS period_end,
    m.manga_count, c.chapter_count, i.image_count, so.source_count, p.people_count
FROM series s
LEFT JOIN LATERAL (
    SELECT count(*) AS manga_count FROM mb_manga
    WHERE deleted_at IS NULL AND created_at >= s.span_s AND created_at <  s.span_e
) m ON true
LEFT JOIN LATERAL (
    SELECT count(*) AS chapter_count FROM mb_chapters
    WHERE deleted_at IS NULL AND created_at >= s.span_s AND created_at <  s.span_e
) c ON true
LEFT JOIN LATERAL (
    SELECT count(*) AS image_count FROM mb_images
    WHERE deleted_at IS NULL AND created_at >= s.span_s AND created_at <  s.span_e
) i ON true
LEFT JOIN LATERAL (
    SELECT count(*) AS source_count FROM mb_sources
    WHERE deleted_at IS NULL AND created_at >= s.span_s AND created_at <  s.span_e
) so ON true
LEFT JOIN LATERAL (
    SELECT count(*) AS people_count FROM mb_people
    WHERE deleted_at IS NULL AND created_at >= s.span_s AND created_at <  s.span_e
) p ON true
ORDER BY s.period, s.span_s";
		return _sql.Get<DatabaseStats>(QUERY);
	}

	public async Task<QueueStats> QueueStats()
	{
		var manga = await _publish.NewManga.Queue.Length();
		var chapters = await _publish.NewChapters.Queue.Length();
		var images = await _publish.NewImages.Queue.Length();
		return new QueueStats
		{
			Timestamp = DateTime.UtcNow,
			Manga = (int)manga,
			Chapters = (int)chapters,
			Images = (int)images
		};
	}

	public async Task TakeSnapshot()
	{
		_dbStats = await DatabaseStats();
		_queueStats.Add(await QueueStats());
	}

	public StatsItem Snapshot => new([.. _queueStats], [.._dbStats ?? []]);
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
	/// The start of the time period
	/// </summary>
	[JsonPropertyName("start")]
	public DateTime PeriodStart { get; set; }

	/// <summary>
	/// The end of the time period
	/// </summary>
	[JsonPropertyName("end")]
	public DateTime PeriodEnd { get; set; }

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
	/// When the snapshot was taken
	/// </summary>
	[JsonPropertyName("timestamp")]
	public DateTime Timestamp { get; set; }

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
/// <param name="Queue">The queue stats</param>
/// <param name="Database">The database stats</param>
public record class StatsItem(
	[property: JsonPropertyName("queue")] QueueStats[] Queue,
	[property: JsonPropertyName("database")] DatabaseStats[] Database);