namespace MangaBox.Match.RIS;

using TaskType = Func<Task<RISSearchResult<MangaMetadata>>>;

internal class MatchSearchService(
	IDbService _db,
	IRISApiService _api,
	ISourceService _sources) : IImageSearchService
{
	public const string SERVICE_SLUG = "match";

	public RISServices Type => RISServices.MatchRIS;

	public static void FixBadMetadata(MatchMetaData<MangaMetadata> meta)
	{
		try
		{
			if (!meta.FilePath.Contains('{'))
				return;

			var metadata = JsonSerializer.Deserialize<MangaMetadata>(meta.FilePath);
			if (metadata is null) return;

			meta.FilePath = RISIndexService.GenerateId(metadata);
			meta.MetaData = metadata;
		}
		catch { }
	}

	public IAsyncEnumerable<ImageSearchResult> Search(string url, CancellationToken token)
	{
		return Search(() => _api.Search<MangaMetadata>(url), token);
	}

	public async IAsyncEnumerable<ImageSearchResult> Search(MemoryStream stream, string fileName,
		[EnumeratorCancellation] CancellationToken token)
	{
		stream.Position = 0;
		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms, token);
		ms.Position = 0;
		await foreach(var result in Search(() => _api.Search<MangaMetadata>(ms, fileName), token))
			yield return result;
	}

	public async IAsyncEnumerable<ImageSearchResult> Search(TaskType request,
		[EnumeratorCancellation] CancellationToken token)
	{
		var response = await request();
		if (response is null || 
			!response.Success || 
			response.Result.Length == 0)
			yield break;

		var sources = await _sources.All(token).ToList(token);

		var bySource = response.Result
			.Select(t =>
			{
				FixBadMetadata(t);
				return t;
			})
			.GroupBy(r => r.MetaData?.Source, StringComparer.OrdinalIgnoreCase);
		foreach(var group in bySource)
		{
			token.ThrowIfCancellationRequested();

			Dictionary<string, MangaBoxType<MbManga>> mangasById = [];
			var source = sources.FirstOrDefault(t => t.Info.Slug.EqualsIc(group.Key));
			if (source is not null)
			{
				var mids = group.Select(t => t.MetaData?.MangaId!)
					.Where(t => !string.IsNullOrEmpty(t))
					.ToArray();
				var manga = await _db.Manga.ByIds(source.Info.Id, mids);
				mangasById = manga.ToDictionary(m => m.Entity.OriginalSourceId, StringComparer.OrdinalIgnoreCase);
			}

			foreach (var manga in group)
				yield return new()
				{
					Source = SERVICE_SLUG,
					Score = manga.Score,
					Exact = manga.Score >= 100,
					Closest = mangasById.GetValueOrDefault(manga.MetaData?.MangaId ?? string.Empty),
					Result = manga.MetaData,
					Image = manga.FilePath,
				};
		}
	}
}
