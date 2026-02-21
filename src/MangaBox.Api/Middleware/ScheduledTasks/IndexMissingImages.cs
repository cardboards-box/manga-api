namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A scheduled task for indexing missing images
/// </summary>
public class IndexMissingImages(
	IDbService _db,
	IImageService _image,
	IMangaPublishService _publish,
	ILogger<IndexMissingImages> _logger) : IInvocable
{
	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			var failedBuffer = DateTime.UtcNow.Subtract(_image.ErrorWaitPeriod);
			var images = await _db.Image.NotIndexed(failedBuffer);
			var queued = (await _publish.NewImages.Queue.All())
				.Select(x => x.Id)
				.Distinct()
				.ToHashSet();

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
