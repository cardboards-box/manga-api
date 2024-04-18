namespace MangaBox.Models;

/// <summary>
/// Represents a source for manga
/// </summary>
[Table("mb_providers")]
public class Provider : DbObject
{
    public const string MANGA_DEX = "MangaDex";
    public const string MANGA_CLASH = "MangaClash";
    public const string MANGA_KATANA = "MangaKatana";
    public const string DARK_SCANS = "DarkScans";
    public const string N_HENTAI_TO = "nhentai.to";

    public const string MANGA_KAKALOT_TV = "Mangakakalot.tv";
    public const string MANGA_KAKALOT_COM = "MangaKakalot.com";
    public const string MANGA_KAKALOT_COM_ALT = "MangaKakalot.com (Alt)";

    /// <summary>
    /// The name of the source
    /// </summary>
    [Column("name", Unique = true)]
    public required string Name { get; set; }

    /// <summary>
    /// The URL of the source (like an API url or something)
    /// </summary>
    [Column("url")]
    public required string Url { get; set; }

    /// <summary>
    /// How the manga is resolved from the source
    /// </summary>
    [Column("type")]
    public required SourceType Type { get; set; }

    /// <summary>
    /// Whether or not to enable and disable the source on the website
    /// </summary>
    [Column("enabled")]
    public required bool Enabled { get; set; }

    /// <summary>
    /// The URL to the home-page of the source
    /// </summary>
    [Column("home_url")]
    public required string HomeUrl { get; set; }

    /// <summary>
    /// The referrer for the source when requesting images
    /// </summary>
    [Column("referrer")]
    public string? Referrer { get; set; }
}