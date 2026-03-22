namespace MangaBox.Api.Middleware.ScheduledTasks;

using Services.Imaging;

/// <summary>
/// A scheduled task for indexing missing images
/// </summary>
public class IndexMissingImages(
	IDbService _db,
	IImageService _image,
	IMangaPublishService _publish,
	ILogger<IndexMissingImages> _logger) : ICancellableInvocable, IInvocable
{
	/// <inheritdoc />
	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			var failedBuffer = DateTime.UtcNow.Subtract(_image.ErrorWaitPeriod);
			var images = await _db.Image.NotIndexed(failedBuffer);
			var queued = await _publish.NewImages.All(CancellationToken)
				.Select(x => x.Id)
				.Distinct()
				.ToHashSetAsync(null, CancellationToken);

			foreach (var image in images)
				if (!queued.Contains(image.Id))
					await _publish.NewImages.Publish(new(image.Id, DateTime.UtcNow, false));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Missing Image Index] Error indexing missing images");
		}
	}
}
