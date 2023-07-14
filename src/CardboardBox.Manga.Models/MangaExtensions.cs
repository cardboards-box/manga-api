using MangaDexSharp;
using MManga = MangaDexSharp.Manga;

namespace CardboardBox.Manga.Models;

public static partial class MangaExtensions
{
    public const string DEFAULT_LANG = "en";
    public const string MANGA_DEX_PROVIDER = "mangadex";
    public const string MANGA_DEX_HOME_URL = "https://mangadex.org";

    public static string GetHashId(this DbManga manga)
    {
        var regex = StripNonAlphaNumeric();
        return regex.Replace($"{manga.Provider} {manga.Title}", "").ToLower();
    }

    public static T Convert<T>(MManga manga) where T: DbManga, new()
    {
        static string DetermineTitle(MManga manga)
        {
            var title = manga.Attributes.Title.PreferedOrFirst(t => t.Key.ToLower() == DEFAULT_LANG);
            if (title.Key.ToLower() == DEFAULT_LANG) return title.Value;

            var prefered = manga.Attributes.AltTitles.FirstOrDefault(t => t.ContainsKey(DEFAULT_LANG));
            if (prefered != null)
                return prefered.PreferedOrFirst(t => t.Key.ToLower() == DEFAULT_LANG).Value;

            return title.Value;
        }

        static IEnumerable<DbMangaAttribute> GetMangaAttributes(MManga? manga)
        {
            if (manga == null) yield break;

            if (manga.Attributes.ContentRating != null)
                yield return new("Content Rating", manga.Attributes.ContentRating?.ToString() ?? "");

            if (!string.IsNullOrEmpty(manga.Attributes.OriginalLanguage))
                yield return new("Original Language", manga.Attributes.OriginalLanguage);

            if (manga.Attributes.Status != null)
                yield return new("Status", manga.Attributes.Status?.ToString() ?? "");

            if (!string.IsNullOrEmpty(manga.Attributes.State))
                yield return new("Publication State", manga.Attributes.State);

            foreach (var rel in manga.Relationships)
            {
                switch (rel)
                {
                    case PersonRelationship person:
                        yield return new(person.Type == "author" ? "Author" : "Artist", person.Attributes.Name);
                        break;
                    case ScanlationGroup group:
                        yield return new("Scanlation Group", group.Attributes.Name);
                        break;
                }
            }
        }

        var id = manga.Id;
        var coverFile = (manga
            .Relationships
            .FirstOrDefault(t => t is CoverArtRelationship) as CoverArtRelationship
        )?.Attributes?.FileName;
        var coverUrl = $"{MANGA_DEX_HOME_URL}/covers/{id}/{coverFile}";

        var title = DetermineTitle(manga);
        var nsfwRatings = new[] { "erotica", "suggestive", "pornographic" };

        var output = new T
        {
            Title = title,
            SourceId = id,
            Provider = MANGA_DEX_PROVIDER,
            Url = $"{MANGA_DEX_HOME_URL}/title/{id}",
            Cover = coverUrl,
            Description = manga.Attributes.Description.PreferedOrFirst(t => t.Key == DEFAULT_LANG).Value,
            AltTitles = manga.Attributes.AltTitles.SelectMany(t => t.Values).Distinct().ToArray(),
            Tags = manga
                .Attributes
                .Tags
                .Select(t =>
                    t.Attributes
                     .Name
                     .PreferedOrFirst(t => t.Key == DEFAULT_LANG)
                     .Value).ToArray(),
            Nsfw = nsfwRatings.Contains(manga.Attributes.ContentRating?.ToString() ?? ""),
            Attributes = GetMangaAttributes(manga).ToArray(),
            SourceCreated = manga.Attributes.CreatedAt
        };
        output.HashId = output.GetHashId();
        return output;
    }

    public static DbManga Convert(MManga manga) => Convert<DbManga>(manga);

    public static DbMangaCache ConvertCache(MManga manga) => Convert<DbMangaCache>(manga);

    public static T Convert<T>(Chapter chapter) where T: DbMangaChapter, new()
    {
        static IEnumerable<DbMangaAttribute> GetChapterAttributes(Chapter? chapter)
        {
            if (chapter == null) yield break;

            yield return new("Translated Language", chapter.Attributes.TranslatedLanguage);

            if (!string.IsNullOrEmpty(chapter.Attributes.Uploader))
                yield return new("Uploader", chapter.Attributes.Uploader);

            foreach (var relationship in chapter.Relationships)
            {
                switch (relationship)
                {
                    case PersonRelationship per:
                        yield return new(per.Type == "author" ? "Author" : "Artist", per.Attributes.Name);
                        break;
                    case ScanlationGroup grp:
                        if (!string.IsNullOrEmpty(grp.Attributes.Name))
                            yield return new("Scanlation Group", grp.Attributes.Name);
                        if (!string.IsNullOrEmpty(grp.Attributes.Website))
                            yield return new("Scanlation Link", grp.Attributes.Website);
                        if (!string.IsNullOrEmpty(grp.Attributes.Twitter))
                            yield return new("Scanlation Twitter", grp.Attributes.Twitter);
                        if (!string.IsNullOrEmpty(grp.Attributes.Discord))
                            yield return new("Scanlation Discord", grp.Attributes.Discord);
                        break;
                }
            }
        }

        return new T
        {
            Title = chapter?.Attributes.Title ?? string.Empty,
            Url = $"{MANGA_DEX_HOME_URL}/chapter/{chapter?.Id}",
            SourceId = chapter?.Id ?? string.Empty,
            Ordinal = double.TryParse(chapter?.Attributes.Chapter, out var a) ? a : 0,
            Volume = double.TryParse(chapter?.Attributes.Volume, out var b) ? b : null,
            ExternalUrl = chapter?.Attributes.ExternalUrl,
            Attributes = GetChapterAttributes(chapter).ToArray(),
            Language = DEFAULT_LANG,
        };
    }

    public static DbMangaChapter Convert(Chapter chapter) => Convert<DbMangaChapter>(chapter);
    
    public static DbMangaChapterCache ConvertCache(Chapter chapter) => Convert<DbMangaChapterCache>(chapter);

    [GeneratedRegex("[^a-zA-Z0-9 ]")]
    private static partial Regex StripNonAlphaNumeric();
}
