using Npgsql;

namespace MangaBox.Cli.Verbs;

using Database;
using Models;
using Models.Composites;
using Services;

using IdMap = Dictionary<int, Guid>;

[Verb("migrate", HelpText = "Migrates the old CBA manga schema to the new MangaBox schema")]
internal class MigrateOptions 
{
	[Value(0, HelpText = "The migration methods to run")]
	public IEnumerable<string> Methods { get; set; } = [];
}

internal class MigrateVerb(
	IDbService _db,
	ISqlService _sql,
	LegacyPostgresSqlService _legacy,
	IMangaLoaderService _loader,
	ILogger<MigrateVerb> logger) : BooleanVerb<MigrateOptions>(logger)
{
	public async Task EnsureSources(CancellationToken token)
	{
		await _loader.Sources(token).ToArrayAsync(token);
	}

	public async Task<(IdMap Manga, IdMap Chapters, IdMap Profiles)> GetLegacyMap()
	{
		const string QUERY = @"
SELECT id, legacy_id FROM mb_manga WHERE legacy_id IS NOT NULL AND legacy_id > 0;
SELECT id, legacy_id FROM mb_chapters WHERE legacy_id IS NOT NULL AND legacy_id > 0;
SELECT id, legacy_id FROM mb_profiles WHERE legacy_id IS NOT NULL AND legacy_id > 0;";

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(QUERY);

		var manga = (await rdr.ReadAsync<LegacyIdMap>()).ToDictionarySafe(t => t.LegacyId, t => t.Id);
		var chapters = (await rdr.ReadAsync<LegacyIdMap>()).ToDictionarySafe(t => t.LegacyId, t => t.Id);
		var profiles = (await rdr.ReadAsync<LegacyIdMap>()).ToDictionarySafe(t => t.LegacyId, t => t.Id);
		return (manga, chapters, profiles);
	}

	public async Task LoadMangaJson(LegacyManga manga, bool logOutput, CancellationToken token)
	{
		try
		{
			var def = JsonSerializer.Deserialize<MangaSource.Manga>(manga.MangaJson);
			if (def is null)
			{
				_logger.LogWarning("Failed to deserialize manga >> {Id}", manga.LegacyId);
				return;
			}

			var source = await _loader.FindSource(manga.MangaUrl, token);
			if (source is null)
			{
				_logger.LogWarning("No source found for manga >> {Id} >> {Url}", manga.LegacyId, manga.MangaUrl);
				return;
			}

			var result = await _loader.Load(def, source.Info.Id, null, new(manga.LegacyId));
			if (!result.Success || result is not Boxed<MangaBoxType<MbManga>> output || output.Data is null)
			{
				_logger.LogWarning("Failed to load manga >> {Id} >> {Description} >> {Errors}", 
					manga.LegacyId, result.Description ?? "Output was not the correct type", string.Join(", ", result.Errors ?? []));
				return;
			}

			if (logOutput) _logger.LogInformation("Successfully loaded manga >> {Id} >> {MangaId}", 
				manga.LegacyId, output.Data.Entity.Id);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while loading manga >> {Id}", manga.LegacyId);
		}
	}

	public async Task LoadManga(bool cache, CancellationToken token)
	{
		await EnsureSources(token);
		var legacy = await (cache ? _legacy.MangaCache() : _legacy.Manga());
		_logger.LogInformation("Fetched {Count} legacy manga entries", legacy.Length);

		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount,
			CancellationToken = token,
		};

		int count = 0;

		await Parallel.ForEachAsync(legacy, opts, async (manga, ct) =>
		{
			await LoadMangaJson(manga, !cache, ct);
			Interlocked.Increment(ref count);

			if (count % 1000 != 0) return;

			_logger.LogInformation("Processed {Count}/{Total} ({Percentage:P2})", count, legacy.Length, (double)count / legacy.Length);
		});

		_logger.LogInformation("Finished!");
	}

	public async Task LoadProfiles()
	{
		var profiles = await _legacy.Profiles();
		_logger.LogInformation("Fetched {Count} legacy profiles", profiles.Length);
		foreach(var profile in profiles)
		{
			await _db.Profile.Upsert(profile);
			_logger.LogInformation("Upserted profile >> {PlatformId}", profile.PlatformId);
		}
		_logger.LogInformation("Finished!");
	}

	public async Task LoadProgress(CancellationToken token)
	{
		var progresses = (await _legacy.Progress())
			.OrderByDescending(t => t.Chapters.Count)
			.ToArray();
		_logger.LogInformation("Fetched {Count} legacy progresses", progresses.Length);

		var (manga, chapters, profiles) = await GetLegacyMap();
		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount,
			CancellationToken = token,
		};

		int count = 0;

		await Parallel.ForEachAsync(progresses, opts, async (item, ct) =>
		{
			if (!manga.TryGetValue(item.MangaId, out var mangaId))
			{
				_logger.LogWarning("No manga mapping found for progress >> {MangaId}", item.MangaId);
				Interlocked.Increment(ref count);
				return;
			}

			if (!profiles.TryGetValue(item.ProfileId, out var profileId))
			{
				_logger.LogWarning("No profile mapping found for progress >> {ProfileId}", item.ProfileId);
				Interlocked.Increment(ref count);
				return;
			}

			Guid? chapterId = chapters.TryGetValue(item.LastReadChapterId ?? 0, out var cid) ? cid : null;

			var progress = await _db.MangaProgress.Upsert(new()
			{
				ProfileId = profileId,
				MangaId = mangaId,
				LastReadOrdinal = item.LastReadOrdinal,
				LastReadChapterId = chapterId,
				LastReadAt = item.LastReadAt,
				IsCompleted = item.IsCompleted,
				Favorited = item.Favorited,
			});

			foreach(var chapter in item.Chapters)
			{
				if (!chapters.TryGetValue(chapter.ChapterId, out var chapId))
				{
					_logger.LogWarning("No chapter mapping found for progress chapter >> {ProfileId} >> {MangaId} >> {ChapterId}", 
						chapter.ProfileId, chapter.MangaId, chapter.ChapterId);
					continue;
				}

				await _db.ChapterProgress.Upsert(new()
				{
					ProgressId = progress,
					ChapterId = chapId,
					PageOrdinal = chapter.PageOrdinal,
					Bookmarks = chapter.Bookmarks,
					LastRead = chapter.LastRead,
				});
			}

			Interlocked.Increment(ref count);
			if (count % 1000 != 0) return;

			_logger.LogInformation("Processed {Count}/{Total} ({Percentage:P2})", 
				count, progresses.Length, (double)count / progresses.Length);
		});

		_logger.LogInformation("Finished!");
	}

	public override async Task<bool> Execute(MigrateOptions options, CancellationToken token)
	{
		(string name, Func<CancellationToken, Task> action)[] actions =
		[
			("load-manga", ct => LoadManga(false, ct)),
			("load-manga-cache", ct => LoadManga(true, ct)),
			("load-profiles", ct => LoadProfiles()),
			("load-progress", LoadProgress),
		];

		var migrations = (options.Methods ?? []).ToArray();
		if (migrations.Length == 0) migrations = [..actions.Select(a => a.name)];

		foreach(var migration in migrations)
		{
			var action = actions.FirstOrDefault(a => a.name == migration);
			if (action.name is null)
			{
				_logger.LogWarning("No migration found for method >> {Method}", migration);
				continue;
			}

			_logger.LogInformation("Starting migration >> {Method}", migration);
			await action.action(token);
			_logger.LogInformation("Finished migration >> {Method}", migration);
		}

		return true;
	}
}

internal class LegacyManga
{
	public int LegacyId { get; set; }

	public string MangaJson { get; set; } = string.Empty;

	public string MangaUrl { get; set; } = string.Empty;
}

internal class LegacyProgress
{
	public int ProfileId { get; set; }

	public int MangaId { get; set; }

	public double? LastReadOrdinal { get; set; }

	public int? LastReadChapterId { get; set; }

	public DateTime? LastReadAt { get; set; }

	public bool IsCompleted { get; set; }

	public bool Favorited { get; set; }

	public List<LegacyProgressChapter> Chapters { get; set; } = [];
}

internal class LegacyProgressChapter
{
	public int ProfileId { get; set; }

	public int MangaId { get; set; }

	public int ChapterId { get; set; }

	public int? PageOrdinal { get; set; }

	public int[] Bookmarks { get; set; } = [];

	public DateTime? LastRead { get; set; }
}

internal class LegacyIdMap
{
	public int LegacyId { get; set; }

	public Guid Id { get; set; }
}

internal class LegacyPostgresSqlService(
	ILogger<LegacyPostgresSqlService> _logger,
	IQueryCacheService _cache,
	IConfiguration _config) : SqlService
{
	public string ConnectionString => _config["LegacyConnectionString"] 
		?? throw new InvalidOperationException("LegacyConnectionString is not present");

	public override int Timeout => 0;

	public Task<MbProfile[]> Profiles()
	{
		const string QUERY = @"SELECT
    id as legacy_id,
    username,
    avatar,
    platform_id,
    provider,
    provider_id,
    settings_blob,
    email,
    admin,
    can_read,
    created_at,
    updated_at
FROM profiles;";
		return Get<MbProfile>(QUERY);
	}

	public async Task<LegacyManga[]> Manga()
	{
		var query = await _cache.Required("Migrations/legacy_manga_fetch");
		return await Get<LegacyManga>(query);
	}

	public async Task<LegacyManga[]> MangaCache()
	{
		var query = await _cache.Required("Migrations/legacy_manga_fetch_cache");
		return await Get<LegacyManga>(query);
	}

	public async Task<LegacyProgress[]> Progress()
	{
		var query = await _cache.Required("Migrations/legacy_manga_progress");
		using var con = await CreateConnection();
		using var rdr = await con.QueryMultipleAsync(query);

		var progresses = (await rdr.ReadAsync<LegacyProgress>()).ToArray();

		foreach(var chapter in await rdr.ReadAsync<LegacyProgressChapter>())
		{
			var progress = progresses.FirstOrDefault(p => 
				p.ProfileId == chapter.ProfileId && 
				p.MangaId == chapter.MangaId);
			if (progress is null)
			{
				_logger.LogWarning("No progress found for chapter >> {ProfileId} >> {MangaId} >> {ChapterId}", 
					chapter.ProfileId, chapter.MangaId, chapter.ChapterId);
				continue;
			}

			progress.Chapters.Add(chapter);
		}

		return progresses;
	}

	public override async Task<IDbConnection> CreateConnection()
	{
		var con = new NpgsqlConnection(ConnectionString);
		await con.OpenAsync();
		return con;
	}
}