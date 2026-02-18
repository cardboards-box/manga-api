namespace MangaBox.Models;

/// <summary>
/// An image for either a chapter or a manga cover
/// </summary>
[Table("mb_images")]
[InterfaceOption(nameof(MbImage))]
public class MbImage : MbDbObject
{
	/// <summary>
	/// The URL to the image
	/// </summary>
	[Url]
	[Column("url")]
	[JsonPropertyName("url")]
	public string Url { get; set; } = string.Empty;

	/// <summary>
	/// The ID of the chapter this image belongs to, if applicable
	/// </summary>
	/// <remarks>If the image is a chapter page, this will be filled in</remarks>
	[Column("chapter_id", Unique = true), Fk<MbChapter>]
	[JsonPropertyName("chapterId")]
	public Guid? ChapterId { get; set; }

	/// <summary>
	/// The ID of the manga this image belongs to
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	public Guid MangaId { get; set; }

	/// <summary>
	/// The ordinal position of the image within its chapter or manga
	/// </summary>
	[Column("ordinal", Unique = true)]
	[JsonPropertyName("ordinal")]
	public int Ordinal { get; set; }

	/// <summary>
	/// The file name of the image
	/// </summary>
	[Column("file_name")]
	[JsonPropertyName("fileName")]
	public string? FileName { get; set; }

	/// <summary>
	/// A hash of the URL
	/// </summary>
	[Column("url_hash")]
	[JsonPropertyName("urlHash")]
	public string? UrlHash { get; set; }

	/// <summary>
	/// The width of the image in pixels
	/// </summary>
	[Column("image_width")]
	[JsonPropertyName("imageWidth")]
	public int? ImageWidth { get; set; }

	/// <summary>
	/// The height of the image in pixels
	/// </summary>
	[Column("image_height")]
	[JsonPropertyName("imageHeight")]
	public int? ImageHeight { get; set; }

	/// <summary>
	/// The size of the image in bytes
	/// </summary>
	[Column("image_size")]
	[JsonPropertyName("imageSize")]
	public long? ImageSize { get; set; }

	/// <summary>
	/// The mime-type of the image
	/// </summary>
	[Column("mime_type")]
	[JsonPropertyName("mimeType")]
	public string? MimeType { get; set; }

	/// <summary>
	/// Whether or not the image has been indexed
	/// </summary>
	[Column("indexed", ExcludeUpdates = true)]
	[JsonPropertyName("indexed")]
	public bool Indexed { get; set; } = false;
}
