namespace MangaBox.Database;

using Services;

/// <summary>
/// The service for interacting with the database
/// </summary>
public interface IDbService
{
	/// <summary>
	/// The service for interacting with the mb_chapters table
	/// </summary>
	IMbChapterDbService Chapter { get; }

	/// <summary>
	/// The service for interacting with the mb_chapter_progress table
	/// </summary>
	IMbChapterProgressDbService ChapterProgress { get; }

	/// <summary>
	/// The service for interacting with the mb_images table
	/// </summary>
	IMbImageDbService Image { get; }

	/// <summary>
	/// The service for interacting with the mb_manga table
	/// </summary>
	IMbMangaDbService Manga { get; }

	/// <summary>
	/// The service for interacting with the mb_manga_ext table
	/// </summary>
	IMbMangaExtDbService MangaExt { get; }

	/// <summary>
	/// The service for interacting with the mb_manga_progress table
	/// </summary>
	IMbMangaProgressDbService MangaProgress { get; }

	/// <summary>
	/// The service for interacting with the mb_manga_relationships table
	/// </summary>
	IMbMangaRelationshipDbService MangaRelationship { get; }

	/// <summary>
	/// The service for interacting with the mb_manga_tags table
	/// </summary>
	IMbMangaTagDbService MangaTag { get; }

	/// <summary>
	/// The service for interacting with the mb_people table
	/// </summary>
	IMbPersonDbService Person { get; }

	/// <summary>
	/// The service for interacting with the mb_profiles table
	/// </summary>
	IMbProfileDbService Profile { get; }

	/// <summary>
	/// The service for interacting with the mb_sources table
	/// </summary>
	IMbSourceDbService Source { get; }

	/// <summary>
	/// The service for interacting with the mb_tags table
	/// </summary>
	IMbTagDbService Tag { get; }
}

internal class DbService(IServiceProvider _provider) : IDbService
{
	#region Lazy Loaded Service Caches
	private IMbChapterDbService? _chapter;
	private IMbChapterProgressDbService? _chapterProgress;
	private IMbImageDbService? _image;
	private IMbMangaDbService? _manga;
	private IMbMangaExtDbService? _mangaExt;
	private IMbMangaProgressDbService? _mangaProgress;
	private IMbMangaRelationshipDbService? _mangaRelationship;
	private IMbMangaTagDbService? _mangaTag;
	private IMbPersonDbService? _person;
	private IMbProfileDbService? _profile;
	private IMbSourceDbService? _source;
	private IMbTagDbService? _tag;
	#endregion

	#region Service Implementations
	public IMbChapterDbService Chapter => _chapter ??= _provider.GetRequiredService<IMbChapterDbService>();
	public IMbChapterProgressDbService ChapterProgress => _chapterProgress ??= _provider.GetRequiredService<IMbChapterProgressDbService>();
	public IMbImageDbService Image => _image ??= _provider.GetRequiredService<IMbImageDbService>();
	public IMbMangaDbService Manga => _manga ??= _provider.GetRequiredService<IMbMangaDbService>();
	public IMbMangaExtDbService MangaExt => _mangaExt ??= _provider.GetRequiredService<IMbMangaExtDbService>();
	public IMbMangaProgressDbService MangaProgress => _mangaProgress ??= _provider.GetRequiredService<IMbMangaProgressDbService>();
	public IMbMangaRelationshipDbService MangaRelationship => _mangaRelationship ??= _provider.GetRequiredService<IMbMangaRelationshipDbService>();
	public IMbMangaTagDbService MangaTag => _mangaTag ??= _provider.GetRequiredService<IMbMangaTagDbService>();
	public IMbPersonDbService Person => _person ??= _provider.GetRequiredService<IMbPersonDbService>();
	public IMbProfileDbService Profile => _profile ??= _provider.GetRequiredService<IMbProfileDbService>();
	public IMbSourceDbService Source => _source ??= _provider.GetRequiredService<IMbSourceDbService>();
	public IMbTagDbService Tag => _tag ??= _provider.GetRequiredService<IMbTagDbService>();
	#endregion
}

