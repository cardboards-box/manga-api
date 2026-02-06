namespace MangaBox.Match.SauceNao;

internal class SauceNaoSearchService(
	IDbService _db,
	ISauceNaoApiService _api,
	ILogger<SauceNaoSearchService> _logger,
	[FromKeyedServices(SauceNaoSearchService.LIMITER_KEY)] RateLimiter _limiter) : IImageSearchService
{
	public const string SERVICE_SLUG = "sauce-nao";
	public const string LIMITER_KEY = "sauceNaoRateLimiter";

	public RISServices Type => RISServices.SauceNao;

	public IAsyncEnumerable<ImageSearchResult> Search(string url, CancellationToken token)
	{
		return Search(() => _api.Get(url), token);
	}

	public async IAsyncEnumerable<ImageSearchResult> Search(MemoryStream stream, string fileName,
		[EnumeratorCancellation] CancellationToken token)
	{
		stream.Position = 0;
		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms, token);
		ms.Position = 0;
		await foreach (var result in Search(() => _api.Get(ms, fileName), token))
			yield return result;
	}

	public async IAsyncEnumerable<ImageSearchResult> Search(Func<Task<Sauce?>> request, 
		[EnumeratorCancellation] CancellationToken token)
	{
		using var lease = await _limiter.AcquireAsync(1, token);
		if (!lease.IsAcquired)
		{
			_logger.LogWarning("Rate limit exceeded for SauceNao");
			yield break;
		}

		var response = await request();
		lease.Dispose();

		if (response is null || 
			response.Results is null || 
			response.Results.Length == 0)
			yield break;

		foreach(var result in response.Results)
		{
			token.ThrowIfCancellationRequested();

			if (!double.TryParse(result.MetaData.Similarity, out var score))
				continue;

			var manga = (await _db.Manga.ByUrls(result.Data.ExternalUrls))
				.FirstOrDefault();

			yield return new ImageSearchResult
			{
				Source = SERVICE_SLUG,
				Score = score,
				Exact = score >= 99,
				Result = result,
				Closest = manga,
				Image = result.MetaData.Thumbnail
			};
		}
	}
}
