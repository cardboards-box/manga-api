namespace MangaBox.Services;

using Suggestions = Dictionary<Guid, ChapterSuggestion>;

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
	/// The keys to use when scanning <see cref="MbChapter.Attributes"/> for similar chapters
	/// </summary>
	private static readonly string[] _chapterKeys = 
	[
		"group", "groupurl", "uploader", 
		"scanlation group", "scanlation discord", 
		"scanlation link", "scanlation twitter"
	];

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

		static double FormatProgress(double progress)
		{
			var result = Math.Clamp(progress, 0, 100);
			return double.Parse(result.ToString("0.00"));
		}

		static double DetermineChapterProgress(ProgressChapter chapter)
		{
			if (chapter.Progress is null || chapter.Progress.PageOrdinal is null) return 0;

			var pageCount = chapter.Chapter.PageCount ?? 0;
			if (chapter.Progress.PageOrdinal >= pageCount) return 100;

			return FormatProgress((double)chapter.Progress.PageOrdinal.Value / pageCount * 100);
		}

		static double BuildPartialProgress(IGrouping<double, ProgressChapter>[] groups)
		{
			if (groups.Length == 0) return 0;

			int currentCount = 0;
			//Iterate through each partial chapter
			foreach (var group in groups)
			{
				//Figure out the progress through the chapter
				var progresses = group.Select(DetermineChapterProgress);
				var progress = progresses.Any() ? progresses.Max() : 0;
				//If the chapter is complete, move to the next one
				if (progress >= 100)
				{
					currentCount++;
					continue;
				}

				//If the chapter hasn't been started and there is no total progress, return 0 progress
				if (progress == 0 && currentCount == 0) 
					return 0;

				//If the chapter hasn't been started but there is progress in other chapters,
				//Determine the number of partial chapters that have been completed, and use
				//that as the overall progress of the chapter
				if (progress == 0)
					return FormatProgress((double)currentCount / groups.Length * 100);

				//If the chapter is in progress, determine the overall progress by taking into account
				var chapterProgress = progress / 100;
				return FormatProgress((currentCount + chapterProgress) / groups.Length * 100);
			}

			return FormatProgress((double)currentCount / groups.Length * 100);
		}

		static VolumeChapter BuildChapter(ProgressChapter[] chapters, ref double? volume)
		{
			volume ??= chapters.PreferredOrFirst(t => t.Chapter.Volume is not null)?.Chapter.Volume;

			var wholes = chapters
				.Where(t => double.IsInteger(t.Chapter.Ordinal))
				.ToArray();

			var partials = chapters
				.Where(t => !double.IsInteger(t.Chapter.Ordinal))
				.GroupBy(t => t.Chapter.Ordinal)
				.OrderBy(t => t.Key)
				.ToArray();

			if (partials.Length > 0 && wholes.Length > 0)
			{
				//This logic handles if the first partial chapter starts with .2
				//If so, we take all of the whole chapters and assume they're a .1 partial
				var firstPartial = partials.First();
				var partialOrdinal = (int)(firstPartial.Key % 1 * 10);
				if (partialOrdinal == 2)
				{
					partials = [
						new FakeGrouping<double, ProgressChapter>(partialOrdinal - 0.1, wholes),
						..partials
					];
					wholes = [];
				}
			}

			var wholeProgress = wholes.Length > 0
				? wholes.Max(DetermineChapterProgress)
				: 0;
			var partialProgress = BuildPartialProgress(partials);
			var progress = Math.Max(wholeProgress, partialProgress);

			return new(
				progress,
				(int)chapters.First().Chapter.Ordinal,
				[.. wholes.Select(t => t.Chapter.Id)],
				[.. partials.Select(t => new VolumeChapterPartial(
					t.Key, [..t.Select(t => t.Chapter.Id)]))]);
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
				t => t.Chapter.Volume, t => (int)Math.Floor(t.Chapter.Ordinal));
			//Shouldn't happen unless something went very wrong.
			if (versions.Length == 0) break;

			current.Add(BuildChapter(versions, ref volume));
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

	/// <summary>
	/// Determines the chapter reading order suggestions from the volume list
	/// </summary>
	/// <param name="volumes">The volumes in the manga</param>
	/// <param name="chapters">The chapters in the manga</param>
	/// <returns>A dictionary of chapter suggestions</returns>
	public static Suggestions DetermineSuggestions(MangaVolume[] volumes, Dictionary<Guid, ProgressChapter> chapters)
	{
		Guid? FindBestMatch(MbChapter chapter, Guid[] available)
		{
			if (available.Length == 0) return null;
			if (available.Length == 1) return available[0];

			var attr = chapter.Attributes
				.Where(t => _chapterKeys.Contains(t.Name, StringComparer.InvariantCultureIgnoreCase))
				.ToArray();
			var ac = available.Select(t => chapters[t]);
			foreach (var chap in ac)
			{
				var cttr = chap.Chapter.Attributes
					.Where(t => _chapterKeys.Contains(t.Name, StringComparer.InvariantCultureIgnoreCase))
					.ToArray();
				var matches = attr.Any(t => cttr.Any(ct => ct.Name.Equals(t.Name, StringComparison.InvariantCultureIgnoreCase)
					&& ct.Value.Equals(t.Value, StringComparison.InvariantCultureIgnoreCase)));
				if (!matches) continue;

				return chap.Chapter.Id;
			}

			return available.First();
		}

		void NextWhole(SuggestionIndicesW indices, MbChapter chapter, Suggestions suggestions)
		{
			int nci = indices.ChapterIndex + 1;
			var volume = volumes[indices.VolumeIndex];
			VolumeChapter nextChapter;
			var type = TransitionType.Chapter;

			if (volume.Chapters.Length <= nci)
			{
				int nvi = indices.VolumeIndex + 1;
				if (volumes.Length <= nvi)
				{
					suggestions.Add(chapter.Id, new(null, TransitionType.End));
					return;
				}

				volume = volumes[nvi];
				nextChapter = volume.Chapters.First();
				type = TransitionType.Volume;
			}
			else nextChapter = volume.Chapters[nci];

			var available = nextChapter.Whole;
			if (nextChapter.Partial.Length > 0)
				available = [..available.Concat(nextChapter.Partial.First().Versions)];

			var match = FindBestMatch(chapter, available);
			if (match is null)
			{
				suggestions.Add(chapter.Id, new(null, TransitionType.End));
				return;
			}

			suggestions.Add(chapter.Id, new(match, type));
		}

		void NextPartial(SuggestionIndicesP indices, MbChapter chapter, Suggestions suggestions)
		{
			int npi = indices.PartialIndex + 1;
			var partials = volumes[indices.VolumeIndex]
				.Chapters[indices.ChapterIndex].Partial;
			if (partials.Length <= npi)
			{
				NextWhole(indices, chapter, suggestions);
				return;
			}

			var available = partials[npi].Versions;
			var match = FindBestMatch(chapter, available);
			if (match is null)
			{
				NextWhole(indices, chapter, suggestions);
				return;
			}

			suggestions.Add(chapter.Id, new(match, TransitionType.Partial));
		}

		int volumeIndex = 0;
		int chapterIndex = 0;

		var output = new Suggestions();

		while(volumeIndex < volumes.Length)
		{
			var volume = volumes[volumeIndex];
			if (volume.Chapters.Length <= chapterIndex)
			{
				volumeIndex++;
				chapterIndex = 0;
				continue;
			}

			var volChap = volume.Chapters[chapterIndex];
			
			for(int i = 0; i < volChap.Whole.Length; i++)
				NextWhole(
					new (volumeIndex, chapterIndex),
					chapters[volChap.Whole[i]].Chapter,
					output);

			for(int i = 0; i < volChap.Partial.Length; i++)
				for(int y = 0; y < volChap.Partial[i].Versions.Length; y++)
					NextPartial(new (volumeIndex, chapterIndex, i),
						chapters[volChap.Partial[i].Versions[y]].Chapter, output);

			chapterIndex++;
		}

		return output;
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
		var progChaps = fixedChapters.ToDictionary(t => t.Chapter.Id);
		var data = new MangaVolumes(progress,
			progChaps,
			volumes,
			DetermineSuggestions(volumes, progChaps));
		return Boxed.Ok(data);
	}

	/// <summary>
	/// A fake instance of <see cref="IGrouping{TKey, TElement}"/>
	/// </summary>
	/// <typeparam name="TK">The type of key</typeparam>
	/// <typeparam name="T">The type of elements</typeparam>
	/// <param name="key">The key of the grouping</param>
	/// <param name="items">The items in the grouping</param>
	internal class FakeGrouping<TK, T>(TK key, IEnumerable<T> items) : List<T>(items), IGrouping<TK, T>
	{
		public TK Key { get; init; } = key;
	}

	internal record class SuggestionIndicesW(int VolumeIndex, int ChapterIndex);

	internal record class SuggestionIndicesP(int VolumeIndex, int ChapterIndex, int PartialIndex)
		: SuggestionIndicesW(VolumeIndex, ChapterIndex);
}
