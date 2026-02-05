using System.Xml.Serialization;

namespace MangaBox.Services.CBZModels;

/// <summary>
/// Represents a comic book page
/// </summary>
public class ComicInfoPage
{
	/// <summary>
	/// The image index (e.g., the file order/index in the archive).
	/// </summary>
	[XmlAttribute("Image")]
	public int Image { get; set; }

	/// <summary>
	/// Strongly-typed page type (mapped from the <c>Type</c> XML attribute).
	/// </summary>
	[XmlIgnore]
	public ComicPageType? PageType { get; set; }

	/// <summary>
	/// Serializer-facing value for <see cref="PageType"/> (and unknown/custom types).
	/// </summary>
	[XmlAttribute("Type")]
	public string? TypeRaw
	{
		get => PageType.HasValue ? ComicPageTypeMap.ToXmlString(PageType.Value) : _typeRaw;
		set
		{
			_typeRaw = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
			PageType = ComicPageTypeMap.TryParse(_typeRaw, out var t) ? t : null;
		}
	}
	private string? _typeRaw;

	/// <summary>
	/// The image file size in bytes.
	/// </summary>
	[XmlIgnore]
	public long? ImageSize { get; set; }

	/// <summary>
	/// Serializer-facing attribute for <see cref="ImageSize"/>.
	/// </summary>
	[XmlAttribute("ImageSize")]
	public string? ImageSizeRaw
	{
		get => LongToString(ImageSize);
		set => ImageSize = StringToLong(value);
	}

	/// <summary>
	/// The image width in pixels.
	/// </summary>
	[XmlIgnore]
	public int? ImageWidth { get; set; }

	/// <summary>
	/// Serializer-facing attribute for <see cref="ImageWidth"/>.
	/// </summary>
	[XmlAttribute("ImageWidth")]
	public string? ImageWidthRaw
	{
		get => IntToString(ImageWidth);
		set => ImageWidth = StringToInt(value);
	}

	/// <summary>
	/// The image height in pixels.
	/// </summary>
	[XmlIgnore]
	public int? ImageHeight { get; set; }

	/// <summary>
	/// Serializer-facing attribute for <see cref="ImageHeight"/>.
	/// </summary>
	[XmlAttribute("ImageHeight")]
	public string? ImageHeightRaw
	{
		get => IntToString(ImageHeight);
		set => ImageHeight = StringToInt(value);
	}

	private static string? IntToString(int? value)
		=> value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null;

	private static int? StringToInt(string? value)
		=> int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

	private static string? LongToString(long? value)
		=> value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null;

	private static long? StringToLong(string? value)
		=> long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
