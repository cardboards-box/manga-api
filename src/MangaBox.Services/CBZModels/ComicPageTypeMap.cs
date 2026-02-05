namespace MangaBox.Services.CBZModels;

/// <summary>
/// Maps between <see cref="ComicPageType"/> and ComicInfo string values.
/// </summary>
internal static class ComicPageTypeMap
{
	// Canonical ComicInfo strings (case-sensitive output; parsing is case-insensitive).
	private static readonly Dictionary<string, ComicPageType> _fromXml =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["FrontCover"] = ComicPageType.FrontCover,
			["InnerCover"] = ComicPageType.InnerCover,
			["BackCover"] = ComicPageType.BackCover,
			["Story"] = ComicPageType.Story,
			["Advertisement"] = ComicPageType.Advertisement,
			["Credits"] = ComicPageType.Credits,
			["TableOfContents"] = ComicPageType.TableOfContents,
			["Letters"] = ComicPageType.Letters,
			["Preview"] = ComicPageType.Preview,
			["Other"] = ComicPageType.Other
		};

	/// <summary>
	/// Attempts to parse a ComicInfo page type string into <see cref="ComicPageType"/>.
	/// </summary>
	public static bool TryParse(string? raw, out ComicPageType type)
	{
		type = default;
		return raw is not null && _fromXml.TryGetValue(raw, out type);
	}

	/// <summary>
	/// Converts a <see cref="ComicPageType"/> into its canonical ComicInfo string.
	/// </summary>
	public static string ToXmlString(ComicPageType type) => type switch
	{
		ComicPageType.FrontCover => "FrontCover",
		ComicPageType.InnerCover => "InnerCover",
		ComicPageType.BackCover => "BackCover",
		ComicPageType.Story => "Story",
		ComicPageType.Advertisement => "Advertisement",
		ComicPageType.Credits => "Credits",
		ComicPageType.TableOfContents => "TableOfContents",
		ComicPageType.Letters => "Letters",
		ComicPageType.Preview => "Preview",
		_ => "Other"
	};
}
