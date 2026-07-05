using SkiaSharp;

namespace MangaBox.Services.Imaging;

/// <summary>
/// Shared helpers for SkiaSharp image decoding, encoding, and bitmap operations.
/// </summary>
public static class SkiaImageHelpers
{
	/// <summary>
	/// Loads an image file into a Skia bitmap.
	/// </summary>
	public static async Task<SKBitmap?> LoadAsync(string path, CancellationToken token = default)
	{
		await using var stream = File.OpenRead(path);
		return await LoadAsync(stream, token);
	}

	/// <summary>
	/// Loads an image stream into a Skia bitmap.
	/// </summary>
	public static async Task<SKBitmap?> LoadAsync(Stream stream, CancellationToken token = default)
	{
		using var data = await ToData(stream, token);
		return SKBitmap.Decode(data);
	}

	/// <summary>
	/// Encodes a bitmap to the given output stream.
	/// </summary>
	public static async Task SaveAsync(SKBitmap bitmap, Stream output, SKEncodedImageFormat format, CancellationToken token, int quality = 90)
	{
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(format, quality) 
			?? throw new InvalidOperationException($"Failed to encode image as {format}");

		await data.AsStream().CopyToAsync(output, token);
	}

	/// <summary>
	/// Encodes a bitmap to the given file path.
	/// </summary>
	public static async Task SaveAsync(SKBitmap bitmap, string path, SKEncodedImageFormat format, CancellationToken token, int quality = 90)
	{
		await using var stream = File.Create(path);
		await SaveAsync(bitmap, stream, format, token, quality);
		await stream.FlushAsync(token);
	}

	/// <summary>
	/// Determines the Skia encoded image format from a file name or MIME type.
	/// </summary>
	public static SKEncodedImageFormat DetermineFormat(string? fileName, string? mimeType = null)
	{
		var ext = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
		return ext switch
		{
			"jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
			"png" => SKEncodedImageFormat.Png,
			"webp" => SKEncodedImageFormat.Webp,
			"gif" => SKEncodedImageFormat.Gif,
			"bmp" => SKEncodedImageFormat.Bmp,
			"ico" => SKEncodedImageFormat.Ico,
			"wbmp" => SKEncodedImageFormat.Wbmp,
			_ => DetermineFormatFromMime(mimeType) ?? SKEncodedImageFormat.Png
		};
	}

	/// <summary>
	/// Determines the Skia encoded image format from a MIME type.
	/// </summary>
	public static SKEncodedImageFormat? DetermineFormatFromMime(string? mimeType)
	{
		return mimeType?.Split(';').FirstOrDefault()?.Trim().ToLowerInvariant() switch
		{
			"image/jpeg" or "image/jpg" => SKEncodedImageFormat.Jpeg,
			"image/png" => SKEncodedImageFormat.Png,
			"image/webp" => SKEncodedImageFormat.Webp,
			"image/gif" => SKEncodedImageFormat.Gif,
			"image/bmp" => SKEncodedImageFormat.Bmp,
			"image/x-icon" or "image/vnd.microsoft.icon" => SKEncodedImageFormat.Ico,
			"image/vnd.wap.wbmp" => SKEncodedImageFormat.Wbmp,
			_ => null
		};
	}

	/// <summary>
	/// Creates a transparent-capable RGBA bitmap.
	/// </summary>
	public static SKBitmap CreateBitmap(int width, int height)
	{
		return new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
	}

	/// <summary>
	/// Crops a bitmap to the given rectangle.
	/// </summary>
	public static SKBitmap Crop(SKBitmap source, SKRectI sourceRect)
	{
		var output = CreateBitmap(sourceRect.Width, sourceRect.Height);
		using var canvas = new SKCanvas(output);
		canvas.Clear(SKColors.Transparent);
		canvas.DrawBitmap(source, sourceRect, new SKRect(0, 0, sourceRect.Width, sourceRect.Height), SKSamplingOptions.Default);
		canvas.Flush();
		return output;
	}

	/// <summary>
	/// Reads a stream into Skia data for decoding.
	/// </summary>
	public static async Task<SKData> ToData(Stream stream, CancellationToken token)
	{
		if (stream is MemoryStream ms && ms.TryGetBuffer(out var buffer))
		{
			var bytes = buffer.Offset == 0 && buffer.Count == buffer.Array!.Length
				? buffer.Array
				: [..buffer.AsSpan()];

			return SKData.CreateCopy(bytes);
		}

		using var memory = new MemoryStream();
		await stream.CopyToAsync(memory, token);
		return SKData.CreateCopy(memory.ToArray());
	}
}
