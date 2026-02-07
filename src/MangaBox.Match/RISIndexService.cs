namespace MangaBox.Match;

using RIS;

/// <summary>
/// A service for indexing images in the RIS database
/// </summary>
public interface IRISIndexService
{
	/// <summary>
	/// Indexes the image by it's ID
	/// </summary>
	/// <param name="id">The ID of the image</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the indexing operation</returns>
	Task<Boxed> Index(Guid id, CancellationToken token);

	/// <summary>
	/// Indexes the given image
	/// </summary>
	/// <param name="image">The image to index</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the indexing operation</returns>
	Task<Boxed> Index(MangaBoxType<MbImage> image, CancellationToken token);
}

/// <inheritdoc cref="IRISIndexService" />
internal class RISIndexService(
	IDbService _db,
	IRISApiService _api,
	IImageService _image,
	ILogger<RISIndexService> _logger) : IRISIndexService
{
	/// <summary>
	/// Generate the metadata for the given image
	/// </summary>
	/// <param name="image">The image</param>
	/// <returns>The metadata</returns>
	public static MangaMetadata GenerateMetaData(MangaBoxType<MbImage> image)
	{
		var source = image.GetItem<MbSource>();
		var manga = image.GetItem<MbManga>();
		var chapter = image.GetItem<MbChapter>();

		return new()
		{
			Id = image.Entity.Url.MD5Hash(),
			Url = image.Entity.Url,
			Source = source?.Slug ?? "unknown",
			Type = image.Entity.ChapterId.HasValue ? MangaMetadataType.Page : MangaMetadataType.Cover,
			MangaId = manga?.OriginalSourceId ?? image.Entity.Id.ToString(),
			ChapterId = chapter?.SourceId ?? image.Entity.ChapterId?.ToString(),
			Page = image.Entity.Ordinal
		};
	}

	/// <summary>
	/// Generates the file ID for the given metadata
	/// </summary>
	/// <param name="metadata">The meta-data for the image</param>
	/// <returns>The file ID</returns>
	public static string GenerateId(MangaMetadata metadata)
	{
		var source = metadata.Source.EqualsIc("mangadex") ? "" : $"{metadata.Source}:";
		return metadata.Type switch
		{
			MangaMetadataType.Page => $"page:{source}{metadata.MangaId}:{metadata.ChapterId}:{metadata.Page}",
			MangaMetadataType.Cover => $"cover:{source}{metadata.MangaId}:{metadata.Id}",
			_ => $"unknown:{source}{metadata.Id}"
		};
	}

	/// <inheritdoc />
	public async Task<Boxed> Index(Guid id, CancellationToken token)
	{
		var image = await _db.Image.FetchWithRelationships(id);
		if (image is null) return Boxed.NotFound(nameof(MbImage), "Image not found");

		return await Index(image, token);
	}

	/// <inheritdoc />
	public async Task<Boxed> Index(MangaBoxType<MbImage> image, CancellationToken token)
	{
		var result = await _image.Get(image, token);
		if (!string.IsNullOrEmpty(result.Error) || result.Stream is null)
		{
			_logger.LogError("Failed to get image stream for image {Id}: {Error}", image.Entity.Id, result.Error);
			return Boxed.Exception(result.Error ?? "Unknown error");
		}

		var metadata = GenerateMetaData(image);
		var fileId = GenerateId(metadata);
		var post = await _api.Add(result.Stream, result.FileName ?? "image.png", fileId, metadata);
		if (!post.Success)
		{
			_logger.LogWarning("Failed to index image {Id} in RIS: {Error}", image.Entity.Id, post.Error);
			return Boxed.Exception(post.Error != null ? string.Join("; ", post.Error) : "Unknown error");
		}

		return Boxed.Ok(metadata);
	}
}
