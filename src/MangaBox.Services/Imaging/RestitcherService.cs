using AsyncKeyedLock;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MangaBox.Services.Imaging;

using ImageStream = IAsyncEnumerable<ImageResult>;

/// <summary>
/// A service for re-stitching and splitting manga pages together
/// </summary>
public interface IRestitcherService
{
	/// <summary>
	/// A request to restitch the chapter
	/// </summary>
	/// <param name="request">The request to restitch the chapter</param>
	/// <param name="token">A cancellation token</param>
	/// <returns>The response</returns>
	Task<Boxed> Restitch(ImageRestitchRequest request, CancellationToken token);

	/// <summary>
	/// Processes a restitch request from the given stream of images
	/// </summary>
	/// <param name="slice">The request</param>
	/// <param name="stream">The stream of images</param>
	/// <param name="token">A cancellation token</param>
	/// <returns>The result of the restitch operation</returns>
	Task<ImageResult> Restitch(MbImage slice, ImageStream stream, CancellationToken token);
}

internal class RestitcherService(
	IDbService _db,
	ICacheService _cache,
	IConfiguration _config,
	ILogger<RestitcherService> _logger) : IRestitcherService
{
	/// <summary>
	/// The mime type of the image
	/// </summary>
	public const string MIME_TYPE = "image/webp";

	/// <summary>
	/// The directory to store restitcher temp files in
	/// </summary>
	public string TempPath => field ??= _config["Imaging:RestitcherTempPath"] ?? Path.Combine(Path.GetTempPath(), "MB-Restitchers");

	/// <inheritdoc />
	public async Task<Boxed> Restitch(ImageRestitchRequest request, CancellationToken token)
	{
		var chapter = await _db.Chapter.FetchWithRelationships(request.ChapterId);
		if (chapter is null)
			return Boxed.NotFound(nameof(MbChapter), "Chapter not found");

		var manga = chapter.GetItem<MbManga>();
		if (manga is null)
			return Boxed.NotFound(nameof(MbManga), "Manga not found");

		var source = chapter.GetItem<MbSource>();
		if (source is null)
			return Boxed.NotFound(nameof(MbSource), "Source not found");

		var images = chapter.GetItems<MbImage>().Select(t => t.Id).ToHashSet();
		if (images is null || images.Count == 0)
			return Boxed.NotFound(nameof(MbImage), "Images not found");

		var stitchIds = request.Images
			.SelectMany(t => t.Slices.Select(s => s.ImageId))
			.ToHashSet();
		if (images.Count != stitchIds.Count || 
			!stitchIds.All(images.Contains) ||
			!images.All(stitchIds.Contains))
			return Boxed.Bad("Not all images are present in the restitch request");

		MbChapter? restitch = null;
		try
		{
			restitch = await RestitchChapter(chapter.Entity);
			int ordinal = 1;
			foreach(var slices in request.Images.OrderBy(t => t.Ordinal))
			{
				var imageSlices = slices.Slices
					.OrderBy(t => t.Ordinal)
					.Select((t, i) => new MbImageSlice
					{
						Image = t.ImageId,
						Start = t.StartY,
						Stop = t.EndY,
						Ordinal = i + 1
					})
					.ToArray();

				if (imageSlices.Length == 0)
					throw new Exception($"No slices found for image {slices.Ordinal} of chapter {chapter.Entity.Id}");

				var image = new MbImage
				{
					ChapterId = restitch.Id,
					MangaId = chapter.Entity.MangaId,
					Url = $"restitch://{restitch.Id}/{slices.Ordinal}",
					Ordinal = ordinal++,
					Headers = [],
					Slices = imageSlices
				};
				image.Id = await _db.Image.Insert(image);
				restitch.PageCount++;
			}

			await _db.Chapter.Update(restitch);
			await _db.MangaExt.Update(chapter.Entity.MangaId);
			var full = await _db.Chapter.FetchWithRelationships(restitch.Id);
			return Boxed.Ok(full);
		}
		catch (Exception ex)
		{
			if (restitch is not null)
				await _db.Chapter.Delete(restitch.Id);
			await _db.MangaExt.Update(chapter.Entity.MangaId);
			_logger.LogError(ex, "Error occurred while restitching chapter {ChapterId} ({Deleted})", 
				chapter.Entity.Id, restitch?.Id.ToString() ?? "No Deleted Chapter");
			return Boxed.Exception(ex);
		}
	}

	/// <inheritdoc />
	public async Task<ImageResult> Restitch(MbImage slice, ImageStream stream, CancellationToken token)
	{
		var path = GetTempPath(slice.Id);
		try
		{
			if (slice.Slices.Length == 0)
				throw new Exception($"No slices found for slice {slice.Id}");

			using var images = new RestitchImages();
			await images.LoadImages(stream, path, token);
			if (images.Count == 0) 
				throw new Exception($"No images found for slice {slice.Id}");

			var neededImages = await slice.Slices.Select(t => t.Image).Distinct()
				.ParallelForeach(async (id, ct) => await images.FetchImage(id, ct), 4, token)
				.ToArrayAsync(token);

			if (neededImages.Length == 0)
				throw new Exception($"No images found for slice {slice.Ordinal} of slice {slice.Id}");

			if (slice.Slices.Length == 1)
				return await SingleSlice(slice, images, token);

			var width = neededImages.Max(t => t.Width);
			var height = slice.Slices.Sum(t => t.Stop - t.Start);
			using var image = new Image<Rgba32>(width, height);
			image.Mutate(t => t.Clear(Color.Transparent));

			int y = 0;
			foreach(var part in slice.Slices.OrderBy(t => t.Ordinal))
			{
				var section = await images.FetchImage(part.Image, token);
				if (section is null)
					return new($"Could not find image for slice: {slice.Id} - {part.Image}", slice);

				var source = new Rectangle(0, part.Start, section.Width, part.Stop - part.Start);
				var dest = new Point(CenterWidth(section.Width, width), y);
				using var cropped = section.Clone(t => t.Crop(source));
				image.Mutate(ctx => ctx.DrawImage(cropped, dest, 1f));
				y += source.Height;
			}

			return await SaveSlice(image, slice, token);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while processing restitch request for slice {SliceId}", slice.Id);
			return new ImageResult($"Error occurred while processing restitch request for slice {slice.Id}", slice);
		}
		finally
		{
			TryDeleteDir(path);
		}
	}

	/// <summary>
	/// Determine the X coord for the image
	/// </summary>
	/// <param name="width">The width of the inbound image</param>
	/// <param name="target">The width of the full image</param>
	/// <returns>The X coordinate to center the image</returns>
	public static int CenterWidth(int width, int target)
	{
		if (width >= target)
			return 0;
		return (target - width) / 2;
	}

	/// <summary>
	/// Saves and returns the output slice
	/// </summary>
	/// <param name="output">The image to save</param>
	/// <param name="slice">The image slice</param>
	/// <param name="token">A cancellation token</param>
	/// <returns>The result of the restitch operation</returns>
	public async Task<ImageResult> SaveSlice(Image output, MbImage slice, CancellationToken token)
	{
		var outPath = _cache.GetCachePath(slice, out var hash, false);
		using var io = File.Create(outPath);
		await output.SaveAsWebpAsync(io, token);
		await io.FlushAsync(token);
		await io.DisposeAsync();

		slice.ImageHeight = output.Height;
		slice.ImageWidth = output.Width;
		slice.ImageSize = new FileInfo(outPath).Length;
		slice.MimeType = MIME_TYPE;
		slice.UrlHash = hash;
		slice.FileName = $"{slice.Id}.webp";
		await _db.Image.Update(slice);
		return new ImageResult(null, slice, File.OpenRead(outPath), false);
	}

	/// <summary>
	/// Handles rendering a single cropped slice
	/// </summary>
	/// <param name="slice">The image to slice</param>
	/// <param name="images">The collection of images to use for slicing</param>
	/// <param name="token">A cancellation token</param>
	/// <returns>The result of the slicing operation</returns>
	public async Task<ImageResult> SingleSlice(MbImage slice, RestitchImages images, CancellationToken token)
	{
		var single = slice.Slices.First();
		using var image = await images.FetchImage(single.Image, token);
		if (image is null)
			return new($"Could not find image for slice: {slice.Id}", slice);

		using var cropped = image.Clone(t => t.Crop(
			new Rectangle(0, single.Start, image.Width, single.Stop - single.Start)));
		return await SaveSlice(cropped, slice, token);
	}

	/// <summary>
	/// Creates a clone of the chapter for the restitch images
	/// </summary>
	/// <param name="chapter">The chapter to clone</param>
	/// <returns>The cloned chapter</returns>
	public async Task<MbChapter> RestitchChapter(MbChapter chapter)
	{
		var created = new MbChapter
		{
			MangaId = chapter.MangaId,
			Title = string.IsNullOrEmpty(chapter.Title) ? "MB - Restitched" : chapter.Title + " (MB - Restitched)",
			Url = chapter.Url,
			SourceId = $"restitched-{chapter.SourceId}",
			Ordinal = chapter.Ordinal,
			Volume = chapter.Volume,
			Language = chapter.Language,
			PageCount = 0,
			ExternalUrl = chapter.ExternalUrl,
			Attributes = chapter.Attributes
		};
		
		created.Id = await _db.Chapter.Insert(created);
		return created;
	}

	/// <summary>
	/// Gets the temporary path for storing restitch files
	/// </summary>
	/// <param name="chapterId">The ID of the chapter the request is for</param>
	/// <returns>The path to the temporary directory for the chapter</returns>
	public string GetTempPath(Guid chapterId)
	{
		if (!Directory.Exists(TempPath))
			Directory.CreateDirectory(TempPath);

		var tempPath = Path.Combine(TempPath, chapterId.ToString());
		if (!Directory.Exists(tempPath))
			Directory.CreateDirectory(tempPath);

		return tempPath;
	}

	/// <summary>
	/// Attempt to delete the directory, ignoring errors
	/// </summary>
	/// <param name="path">The path to the directory</param>
	public void TryDeleteDir(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, true);
		}
		catch (Exception ex)
		{
#if DEBUG
			_logger.LogError(ex, "Error occurred while deleting directory: {Path}", path);
#endif
		}
	}

	/// <summary>
	/// A helper class for keeping track of images
	/// </summary>
	public class RestitchImages : IDisposable
	{
		private static readonly AsyncKeyedLocker<Guid> _cacheLocks = new();
		private readonly ConcurrentDictionary<Guid, string> _imagePaths = [];
		private readonly ConcurrentDictionary<Guid, Image> _images = [];
		private const string IMAGE_DIR = "raw-images";

		/// <summary>
		/// The number of images stored
		/// </summary>
		public int Count => _imagePaths.Count;

		/// <summary>
		/// Fetches the image by it's ID
		/// </summary>
		/// <param name="id">The ID of the image</param>
		/// <param name="token">A cancellation token</param>
		/// <returns>The image</returns>
		/// <exception cref="FileNotFoundException">Thrown if the image could not be found</exception>
		public async Task<Image> FetchImage(Guid id, CancellationToken token)
		{
			if (_images.TryGetValue(id, out var image))
				return image;

			if (!_imagePaths.TryGetValue(id, out var path))
				throw new FileNotFoundException($"Image with ID {id} not found in restitch images");

			using var cacheLock = await _cacheLocks.LockAsync(id, token);
			image = await Image.LoadAsync(path, token);
			_images[id] = image;
			return image;
		}

		/// <summary>
		/// Loads all of the images into the helper
		/// </summary>
		/// <param name="stream">The stream of the images</param>
		/// <param name="path">The path to store the images</param>
		/// <param name="token">A cancellation token</param>
		public async Task LoadImages(ImageStream stream, string path, CancellationToken token)
		{
			var opts = new ParallelOptions
			{
				MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1),
				CancellationToken = token
			};
			await Parallel.ForEachAsync(stream, opts, async (image, ct) =>
			{
				using var cacheLock = await _cacheLocks.LockAsync(image.Image.Id, ct);
				if (image.Stream is null)
					throw new FileNotFoundException($"Image could not be found: {image.Image.Id}: {image.Error ?? "Stream was empty"}");

				var imagePath = BuildImagePath(path, image);
				_imagePaths[image.Image.Id] = imagePath;

				using var io = File.Create(imagePath);
				await image.Stream.CopyToAsync(io, token);
				await io.FlushAsync(token);
				image.Dispose();
			});
		}

		/// <summary>
		/// Gets the extension for the given image result
		/// </summary>
		/// <param name="result">The image result to use</param>
		/// <returns>The file extension for the image</returns>
		/// <exception cref="Exception">Thrown if the file extension could not be determined</exception>
		public static string GetImageExtension(ImageResult result)
		{
			if (!string.IsNullOrEmpty(result.FileName))
			{
				var ext = Path.GetExtension(result.FileName);
				if (!string.IsNullOrEmpty(ext))
					return ext.TrimStart('.');
			}

			if (!string.IsNullOrEmpty(result.MimeType))
			{
				var ext = MimeTypes.GetMimeTypeExtensions(result.MimeType)?.FirstOrDefault();
				if (!string.IsNullOrEmpty(ext))
					return ext.TrimStart('.');
			}

			throw new Exception($"Could not determine file extension for image {result.Image.Id}");
		}

		/// <summary>
		/// Builds the path to store the image in the temp files
		/// </summary>
		/// <param name="path">The temp directory path</param>
		/// <param name="result">The image result to use</param>
		/// <returns>The full path to store the image</returns>
		public static string BuildImagePath(string path, ImageResult result)
		{
			var key = MangaSearchFilter.TableSuffix();
			var dir = Path.Combine(path, IMAGE_DIR, key);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			var ext = GetImageExtension(result);
			return Path.Combine(dir, $"{result.Image.Id}.{ext}");
		}

		/// <inheritdoc />
		public void Dispose()
		{
			foreach (var image in _images.Values)
			{
				image.Dispose();
			}
			_images.Clear();
			_imagePaths.Clear();
			GC.SuppressFinalize(this);
		}
	}
}

/// <summary>
/// A slice of an image to build
/// </summary>
/// <param name="Ordinal">The ordinal of the slice in the image</param>
/// <param name="ImageId">The ID of the image</param>
/// <param name="StartY">The starting Y coordinate of the slice</param>
/// <param name="EndY">The ending Y coordinate of the slice</param>
public record class ImageSlice(
	[property: JsonPropertyName("ordinal")] int Ordinal,
	[property: JsonPropertyName("imageId")] Guid ImageId,
	[property: JsonPropertyName("startY")] int StartY,
	[property: JsonPropertyName("endY")] int EndY);

/// <summary>
/// The image to build from slices
/// </summary>
/// <param name="Ordinal">The ordinal of the image in the chapter</param>
/// <param name="Slices">The slices of the original image to create an image from</param>
public record class ImageSliceImage(
	[property: JsonPropertyName("ordinal")] int Ordinal,
	[property: JsonPropertyName("slices")] ImageSlice[] Slices);

/// <summary>
/// Represents a request to restitch a chapter
/// </summary>
/// <param name="ChapterId">The ID of the chapter this request belongs to</param>
/// <param name="Images">The images to be restitched</param>
public record class ImageRestitchRequest(
	[property: JsonPropertyName("chapterId")] Guid ChapterId,
	[property: JsonPropertyName("images")] ImageSliceImage[] Images);