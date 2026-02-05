using System.Xml.Serialization;

namespace MangaBox.Services.CBZModels;

using static ComicInfoXmlHelpers;

/// <summary>
/// Represents a ComicInfo.xml document (commonly embedded in CBZ/CBR files).
/// </summary>
[XmlRoot("ComicInfo")]
public class ComicInfo
{
	private int? _pageCount;

	/// <summary>
	/// The title of the book/issue.
	/// </summary>
	[XmlElement("Title")]
	public string? Title { get; set; }

	/// <summary>
	/// A summary/description of the book.
	/// </summary>
	[XmlElement("Summary")]
	public string? Summary { get; set; }

	/// <summary>
	/// The issue/volume number (often displayed as "Number").
	/// </summary>
	[XmlElement("Number")]
	public double? Number { get; set; }

	/// <summary>
	/// Total count of issues/volumes in the set/series (often displayed as "Count").
	/// </summary>
	[XmlElement("Count")]
	public int? Count { get; set; }

	/// <summary>
	/// Release/publication year.
	/// </summary>
	[XmlElement("Year")]
	public int? Year { get; set; }

	/// <summary>
	/// Release/publication month (1-12).
	/// </summary>
	[XmlElement("Month")]
	public int? Month { get; set; }

	/// <summary>
	/// Writer/author name(s).
	/// </summary>
	[XmlElement("Writer")]
	public string? Writer { get; set; }

	/// <summary>
	/// Publisher name.
	/// </summary>
	[XmlElement("Publisher")]
	public string? Publisher { get; set; }

	/// <summary>
	/// Genre(s) as a string (often a comma-separated list).
	/// </summary>
	[XmlElement("Genre")]
	public string? Genre { get; set; }

	/// <summary>
	/// Whether the book is black and white.
	/// </summary>
	/// <remarks>
	/// The XML commonly encodes this as "Yes/No" (and sometimes True/False or 1/0).
	/// </remarks>
	[XmlIgnore]
	public bool? BlackAndWhite { get; set; }

	/// <summary>
	/// Serializer-facing value for <see cref="BlackAndWhite"/>. Accepts Yes/No, True/False, 1/0.
	/// </summary>
	[XmlElement("BlackAndWhite")]
	public string? BlackAndWhiteRaw
	{
		get => BoolToString(BlackAndWhite);
		set => BlackAndWhite = StringToBool(value);
	}

	/// <summary>
	/// Whether the book is manga style.
	/// </summary>
	/// <remarks>
	/// The XML commonly encodes this as "Yes/No" (and sometimes True/False or 1/0).
	/// </remarks>
	[XmlIgnore]
	public bool? Manga { get; set; }

	/// <summary>
	/// Serializer-facing value for <see cref="Manga"/>. Accepts Yes/No, True/False, 1/0.
	/// </summary>
	[XmlElement("Manga")]
	public string? MangaRaw
	{
		get => BoolToString(Manga);
		set => Manga = StringToBool(value);
	}

	/// <summary>
	/// Characters (often a comma-separated list).
	/// </summary>
	[XmlElement("Characters")]
	public string? Characters { get; set; }

	/// <summary>
	/// The number of pages.
	/// </summary>
	/// <remarks>
	/// This value is automatically computed from <see cref="Pages"/> when not explicitly present,
	/// and when serializing we always emit the computed value.
	/// </remarks>
	[XmlIgnore]
	public int PageCount
	{
		get => _pageCount ?? (Pages?.Count ?? 0);
		set => _pageCount = value;
	}

	/// <summary>
	/// Serializer-facing value for <see cref="PageCount"/>. When serializing, emits the computed page count.
	/// When deserializing, stores the provided value.
	/// </summary>
	[XmlElement("PageCount")]
	public int PageCountRaw
	{
		get => Pages?.Count ?? _pageCount ?? 0;
		set => _pageCount = value;
	}

	/// <summary>
	/// The list of pages and their metadata.
	/// </summary>
	[XmlArray("Pages")]
	[XmlArrayItem("Page")]
	public List<ComicInfoPage> Pages { get; set; } = [];
}

