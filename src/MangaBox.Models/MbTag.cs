namespace MangaBox.Models;

/// <summary>
/// Represents all of the tags MangaBox supports
/// </summary>
[Table("mb_tags")]
[InterfaceOption(nameof(MbTag))]
public partial class MbTag : MbDbObject
{
	/// <summary>
	/// The character to use to for slugs
	/// </summary>
	public const char SLUG = '-';

	private string? _slug;

	/// <summary>
	/// The slug of the chapter
	/// </summary>
	[Column("slug", Unique = true)]
	[JsonPropertyName("slug"), MinLength(1), Required]
	public string Slug
	{
		get => _slug ??= GenerateSlug(Name);
		set => _slug = GenerateSlug(value);
	}

	/// <summary>
	/// The name of the tag
	/// </summary>
	[Column("name")]
	[JsonPropertyName("name"), MinLength(1), Required]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The description of the tag
	/// </summary>
	[Column("description")]
	[JsonPropertyName("description")]
	public string? Description { get; set; }

	/// <summary>
	/// The source that originally loaded this tag
	/// </summary>
	[Column("source_id"), Fk<MbSource>(ignore: true)]
	[JsonPropertyName("sourceId")]
	public Guid SourceId { get; set; }

	/// <summary>
	/// Converts the given name to the appropriate slug
	/// </summary>
	/// <param name="name">The name to fix</param>
	/// <returns>The slug</returns>
	public static string GenerateSlug(string name)
	{
		name = NonAlphaNumericRegex().Replace(name, SLUG.ToString());
		while (name.Contains($"{SLUG}{SLUG}"))
			name = name.Replace($"{SLUG}{SLUG}", SLUG.ToString());
		return name.Trim(SLUG).ToLower();
	}

	[GeneratedRegex(@"[^a-zA-Z0-9]+")]
	private static partial Regex NonAlphaNumericRegex();
}
