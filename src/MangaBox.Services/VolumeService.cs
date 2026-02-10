namespace MangaBox.Services;

/// <summary>
/// A service for handling volume related operations
/// </summary>
public interface IVolumeService
{
	/// <summary>
	/// Fetches a manga by it's ID
	/// </summary>
	/// <param name="filter">The filter information</param>
	/// <param name="profileId">The ID of the user fetching the manga</param>
	/// <returns>The volume information or the error</returns>
	Task<Boxed> Get(VolumeFilter filter, Guid? profileId);
}

/// <inheritdoc cref="IVolumeService" />
internal class VolumeService(
	IDbService _db) : IVolumeService
{
	/// <summary>
	/// Sorts all of the chapters and progress into a collection and fixes the volume numbers
	/// </summary>
	/// <param name="chapters">The chapters to sort and fix</param>
	/// <param name="progress">The progress data for the chapters</param>
	/// <returns>A collection of progress chapters with fixed volume numbers</returns>
	public static IEnumerable<ProgressChapter> CombineProgress(
		IEnumerable<MbChapter> chapters, 
		Dictionary<Guid, MbChapterProgress> progress)
	{
		var grouped = chapters.GroupBy(t => t.Ordinal);
		foreach(var group in grouped)
		{
			var volume = group.Select(t => t.Volume)
				.Where(t => t.HasValue)
				.OrderBy(t => t!.Value)
				.FirstOrDefault();
			foreach(var chapter in group)
			{
				chapter.Volume ??= volume;
				progress.TryGetValue(chapter.Id, out var chapProgress);
				yield return new(chapter, chapProgress);
			}
		}
	}

	/// <summary>
	/// Attempts to fix gaps in volume numbers by propagating the previous volume number forward
	/// </summary>
	/// <param name="chapters">The chapters to process</param>
	/// <param name="start">The index to start at</param>
	public static void FixVolumeGaps(ProgressChapter[] chapters, int start = 0)
	{
		//Get the first chapter that has a null volume
		var nullVol = chapters.IndexOf(t => !t.Chapter.Volume.HasValue, start);
		//If the chapter doesn't exist or it's less than the start, skip processing
		if (nullVol < start || nullVol <= 0) return;

		//Get the previous volume number
		var previousIdx = chapters.IndexOfBefore(t => t.Chapter.Volume.HasValue, nullVol);
		if (previousIdx == -1)
		{
			FixVolumeGaps(chapters, nullVol + 1);
			return;
		}
		var previous = chapters[previousIdx].Chapter.Volume!.Value;
		//Get next chapter index where the volume numbers are the same
		var next = chapters.IndexOf(t => t.Chapter.Volume == previous, nullVol);
		//If the chapter doesn't exist, move to the next one
		if (next < nullVol)
		{
			FixVolumeGaps(chapters, nullVol + 1);
			return;
		}

		//Set all of the null volume chapters to the previous volume number
		for (var i = nullVol; i < next; i++)
			chapters[i].Chapter.Volume = previous;

		//Recurse to the next chapter
		FixVolumeGaps(chapters, next + 1);
	}

	/// <summary>
	/// Attempts to fix chapters that have null volume numbers but come before other volumes
	/// </summary>
	/// <param name="chapters">The chapters to process</param>
	/// <param name="start">The index to start at</param>
	public static void FixHangingVolumes(ProgressChapter[] chapters, int start = 0)
	{
		//Get the first chapter that has a null volume
		var nullVol = chapters.IndexOf(t => !t.Chapter.Volume.HasValue, start);
		//If the chapter doesn't exist or it's less than the start, skip processing
		if (nullVol < start || nullVol <= 0) return;

		//Find the next chapter that has a volume number
		var next = chapters.IndexOf(t => t.Chapter.Volume.HasValue, nullVol);
		if (next < 0) return;
		//Get the previous volume that has a volume number
		var previousIdx = chapters.IndexOfBefore(t => t.Chapter.Volume.HasValue, nullVol);
		//Determine the volume number to use - If there is no previous index then use the next volume number
		var volume = previousIdx == -1
			? chapters[next].Chapter.Volume!.Value
			: chapters[previousIdx].Chapter.Volume!.Value;
		//Set all of the null volumes to the volume number
		for(var i = nullVol; i < next; i++)
			chapters[i].Chapter.Volume = volume;
		//Recurse to the next chapter
		FixHangingVolumes(chapters, next + 1);
	}

	/// <summary>
	/// Orders the chapters based on the filter data
	/// </summary>
	/// <param name="chapters">The manga chapters</param>
	/// <param name="filter">The filter data</param>
	/// <param name="reset">Whether or not the chapter ordinals reset on new volumes</param>
	/// <returns>The ordered chapters</returns>
	public static IEnumerable<ProgressChapter> Order(
		IEnumerable<ProgressChapter> chapters, VolumeFilter filter, bool reset)
	{
		IEnumerable<ProgressChapter> OrdinalAsc()
		{
			return reset
				? chapters
					.OrderBy(t => t.Chapter.Volume ?? 99999)
					.ThenBy(t => t.Chapter.Ordinal)
				: chapters.OrderBy(t => t.Chapter.Ordinal);
		}

		IEnumerable<ProgressChapter> OrdinalDesc()
		{
			return reset
				? chapters
					.OrderByDescending(t => t.Chapter.Volume ?? 9999)
					.ThenByDescending(t => t.Chapter.Ordinal)
				: chapters.OrderByDescending(t => t.Chapter.Ordinal);
		}

		IEnumerable<ProgressChapter> Read()
		{
			if (chapters.Any(t => t.Progress?.PageOrdinal is not null))
				return filter.Asc ? OrdinalAsc() : OrdinalDesc();

			IOrderedEnumerable<ProgressChapter> start;

			if (filter.Asc)
			{
				start = chapters.OrderBy(t => t.Progress?.PageOrdinal is not null);
				if (reset)
					start = start.ThenBy(t => t.Chapter.Volume ?? 99999);
				return start.ThenBy(t => t.Chapter.Ordinal);
			}

			start = chapters.OrderByDescending(t => t.Progress?.PageOrdinal is not null);
			if (reset)
				start = start.ThenByDescending(t => t.Chapter.Volume ?? 99999);
			return start.ThenByDescending(t => t.Chapter.Ordinal);
		}

		return filter.Order switch
		{
			ChapterOrderBy.Date => filter.Asc
				? chapters.OrderBy(t => t.Chapter.CreatedAt)
				: chapters.OrderByDescending(t => t.Chapter.CreatedAt),
			ChapterOrderBy.Language => filter.Asc
				? chapters.OrderBy(t => t.Chapter.Language)
				: chapters.OrderByDescending(t => t.Chapter.Language),
			ChapterOrderBy.Title => filter.Asc
				? chapters.OrderBy(t => t.Chapter.Title)
				: chapters.OrderByDescending(t => t.Chapter.Title),
			ChapterOrderBy.Read => Read(),
			_ => filter.Asc ? OrdinalAsc() : OrdinalDesc(),
		};
	}

	/// <summary>
	/// Determines the volumes of the chapter
	/// </summary>
	/// <param name="chapters">The chapters</param>
	/// <returns>The volumes of the manga</returns>
	public static IEnumerable<MangaVolume> DetermineVolumes(IEnumerable<ProgressChapter> chapters)
	{
		static MangaVolume ToVolume(List<VolumeChapter> chapters, double? ordinal)
		{
			var state = chapters.All(t => t.Progress >= 100)
				? VolumeState.Completed : chapters.Any(t => t.Progress > 0)
				? VolumeState.InProgress : VolumeState.NotStarted;
			return new(ordinal, state, [..chapters]);
		}

		static double DetermineChapterProgress(ProgressChapter chapter)
		{
			if (chapter.Progress is null || chapter.Progress.PageOrdinal is null) return 0;

			var pageCount = chapter.Chapter.PageCount ?? 0;
			if (chapter.Progress.PageOrdinal >= pageCount) return 100;

			var result = Math.Clamp((double)chapter.Progress.PageOrdinal.Value / pageCount * 100, 0, 100);
			return double.Parse(result.ToString("0.00"));
		}

		var iterator = chapters.GetEnumerator();

		List<VolumeChapter> current = [];
		double? volume = null;
		ProgressChapter? chapter = null;

		while(true)
		{
			//Ensure its not the EoS
			if (chapter is null && !iterator.MoveNext()) break;

			//Get the current chapter
			chapter = iterator.Current;
			//Get all of the grouped versions
			var (versions, last, index) = iterator.MoveUntil(chapter, 
				t => t.Chapter.Volume, t => t.Chapter.Ordinal);
			//Shouldn't happen unless something went very wrong.
			if (versions.Length == 0) break;
			//Get the primary chapter to use for progress
			var firstChap = versions
				.OrderByDescending(t => t.Progress?.PageOrdinal ?? 0)
				.First();
			//Lazily set the volume number
			volume ??= versions.PreferredOrFirst(t => t.Chapter.Volume is not null)?.Chapter.Volume;
			//Check to see if the current chapter has been read
			var chap = new VolumeChapter(
				DetermineChapterProgress(firstChap),
				firstChap.Chapter.Ordinal,
				[.. versions.Select(t => t.Chapter.Id)]);
			current.Add(chap);

			chapter = last;

			if (index != 0) continue;

			//New volume has started
			yield return ToVolume(current, volume);
			current.Clear();
			volume = null;
		}

		if (current.Count == 0) yield break;

		yield return ToVolume(current, volume);
	}

	/// <inheritdoc />
	public async Task<Boxed> Get(VolumeFilter filter, Guid? profileId)
	{
		var entity = await _db.MangaExt.ByManga(filter.MangaId, profileId);
		var manga = entity?.GetItem<MbManga>();
		if (entity is null || entity.Entity is null || manga is null)
			return Boxed.NotFound(nameof(MbManga), "Manga not found");

		var chapters = entity.GetItems<MbChapter>().ToArray();
		if (chapters.Length == 0)
			return Boxed.NotFound(nameof(MbChapter), "No chapters found for manga");

		var progress = entity.GetItem<MbMangaProgress>();
		var chapProgress = entity.GetItems<MbChapterProgress>()
			.ToDictionary(cp => cp.ChapterId, cp => cp);

		var fixedChapters = CombineProgress(chapters, chapProgress)
			.OrderBy(t => t.Chapter.Ordinal)
			.ToArray();
		var reset = manga.OrdinalVolumeReset;
		if (!reset)
		{
			FixVolumeGaps(fixedChapters);
			FixHangingVolumes(fixedChapters);
		}
		var ordered = Order(fixedChapters, filter, reset);
		var volumes = DetermineVolumes(ordered).ToArray();
		var data = new MangaVolumes(progress,
			fixedChapters.ToDictionary(t => t.Chapter.Id),
			volumes);
		return Boxed.Ok(data);
	}
}
