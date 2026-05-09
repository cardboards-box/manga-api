namespace MangaBox.Database;

using Models;
using Models.Composites;

/// <summary>
/// A service for publishing updates to manga
/// </summary>
public interface IMangaPublishService : IDisposable
{
	/// <summary>
	/// The queue for new chapters
	/// </summary>
	IRedisQueue<MbChapter> NewChapters { get; }

	/// <summary>
	/// The queue for new images
	/// </summary>
	IRedisQueue<QueueImage> NewImages { get; }

	/// <summary>
	/// The queue for new manga
	/// </summary>
	IRedisQueue<MangaBoxType<MbManga>> NewManga { get; }

	/// <summary>
	/// Init all of the queues
	/// </summary>
	Task Init();

	/// <summary>
	/// Determine whether or not the chapter can be pushed
	/// </summary>
	/// <param name="mangaId">The ID of the manga</param>
	/// <returns>Whether or not the chapter can be pushed</returns>
	Task<bool> CanPushManga(Guid mangaId);
}

/// <summary>
/// The contents of the image queue
/// </summary>
/// <param name="Id">The ID of the image</param>
/// <param name="Created">The date and time the image was queued</param>
/// <param name="Force">Whether or not to force indexing of the image</param>
public record class QueueImage(
	[property: JsonPropertyName("id")] Guid Id,
	[property: JsonPropertyName("created")] DateTime Created,
	[property: JsonPropertyName("force")] bool? Force = null);
