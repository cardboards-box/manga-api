using MangaDexSharp;
using Rating = MangaDexSharp.ContentRating;
using PerRel = MangaDexSharp.PersonRelationship;

namespace MangaBox.Providers.ThirdParty.MangaDex;

using Models;

public interface IMangaDexInteropService
{
    Task<(Series? series, string? error)> GetManga(string id, Provider provider);
}

internal class MangaDexInteropService(
    IDbService _db,
    IMangaDex _api) : IMangaDexInteropService
{
    public const string DEFAULT_LANG = "en";

    public async Task<ContentRating> GetRating(Rating? rating)
    {
        var key = rating switch 
        { 
            Rating.suggestive => ContentRating.SUGGESTIVE,
            Rating.erotica => ContentRating.EROTICA,
            Rating.pornographic => ContentRating.PORNOGRAPHIC,
            _ => ContentRating.SAFE
        };

        return (await _db.ContentRatings.ByName(key))!;
    }

    public static SeriesStatus GetStatus(Status? status)
    {
        return status switch
        {
            Status.completed => SeriesStatus.Completed,
            Status.hiatus => SeriesStatus.Hiatus,
            Status.cancelled => SeriesStatus.Cancelled,
            _ => SeriesStatus.OnGoing,
        };
    }

    public async Task<Image?> GetCover(Manga manga, Provider provider)
    {
        var rel = manga.Relationships.FirstOrDefault(r => r is CoverArtRelationship);
        if (rel is null || rel is not CoverArtRelationship coverArt) return null;

        var filename = coverArt.Attributes?.FileName;
        if (string.IsNullOrEmpty(filename)) return null;

        var url = $"https://mangadex.org/covers/{manga.Id}/{filename}";
        var image = new Image
        {
            ProviderId = provider.Id,
            Url = url,
            UrlHash = url.MD5Hash(),
            Type = ImageType.Cover,
            Name = filename,
        };
        image.Id = await _db.Images.Upsert(image);
        return image;
    }

    public static string GetTitle(Manga manga)
    {
        var title = manga.Attributes?.Title?.PreferedOrFirst(t => t.Key.EqualsIc(DEFAULT_LANG));
        if (title?.Key.ToLower() == DEFAULT_LANG) return title!.Value.Value;

        var preferred = manga.Attributes?.AltTitles.FirstOrDefault(t => t.ContainsKey(DEFAULT_LANG));
        if (preferred != null)
            return preferred.PreferedOrFirst(t => t.Key.ToLower() == DEFAULT_LANG).Value;

        return title!.Value.Value;
    }

    public async IAsyncEnumerable<Tag> GetTags(Manga manga)
    {
        var tags = await _db.Tags.Get();
        foreach(var tag in manga.Attributes?.Tags ?? [])
        {
            var name = tag.Attributes?.Name.PreferedOrFirst(t => t.Key.EqualsIc(DEFAULT_LANG)).Value;
            if (string.IsNullOrEmpty(name)) continue;

            var dbTag = tags.FirstOrDefault(t => t.Name.EqualsIc(name));
            if (dbTag is not null)
            {
                yield return dbTag;
                continue;
            }

            var newTag = new Tag
            {
                Name = name.ToLower(),
                Display = name,
                Explicit = tag.Attributes?.Group == MangaDexSharp.Group.content
            };
            newTag.Id = await _db.Tags.Upsert(newTag);
            yield return newTag;
        }
    }

    public async Task<Series?> GetSeries(Manga manga, Provider provider)
    {
        var series = new Series
        {
            ProviderId = provider.Id,
            SourceId = manga.Id,
            CoverId = (await GetCover(manga, provider))?.Id,
            RatingId = (await GetRating(manga.Attributes?.ContentRating)).Id,
            Tags = (await GetTags(manga)
                .ToArrayAsync())
                .Select(t => t.Id)
                .ToArray(),
            Title = GetTitle(manga),
            DisplayTitle = null,
            AltTitles = [.. manga.Attributes?.AltTitles.SelectMany(t => t.Values)],
            Description = manga.Attributes?.Description.PreferedOrFirst(t => t.Key == DEFAULT_LANG).Value ?? string.Empty,
            Url = $"https://mangadex.org/title/{manga.Id}",
            Status = GetStatus(manga.Attributes?.Status),
            OrdinalsReset = manga.Attributes?.ChapterNumbersResetOnNewVolume ?? false,
            SourceCreated = manga.Attributes?.CreatedAt ?? DateTime.UtcNow,
        };

        series.Id = await _db.Series.Upsert(series);
        return series;
    }

    public async IAsyncEnumerable<Person> GetRelationships(IRelationship[] relationships, Provider provider)
    {
        IEnumerable<ExternalLink> AuthorLinks(PerRel rel)
        {
            Func<(string? value, string type)>[] items = 
            [
                () => (rel.Attributes?.Twitter, "Twitter"),
                () => (rel.Attributes?.Pixiv, "Pixiv"),
                () => (rel.Attributes?.MelonBook, "MelonBook"),
                () => (rel.Attributes?.FanBox, "FanBox"),
                () => (rel.Attributes?.Booth, "Booth"),
                () => (rel.Attributes?.NicoVideo, "NicoVideo"),
                () => (rel.Attributes?.Skeb, "Skeb"),
                () => (rel.Attributes?.Fantia, "Fantia"),
                () => (rel.Attributes?.Tumblr, "Tumblr"),
                () => (rel.Attributes?.Youtube, "YouTube"),
                () => (rel.Attributes?.Weibo, "Weibo"),
                () => (rel.Attributes?.Naver, "Naver"),
                () => (rel.Attributes?.Website, "Website"),
            ];

            foreach(var item in items)
            {
                var (value, type) = item();
                if (string.IsNullOrEmpty(value)) continue;

                yield return new ExternalLink
                {
                    Platform = type,
                    Url = value,
                };
            }
        }

        async Task<Person?> AuthorOrArtist(PerRel rel)
        {
            if (string.IsNullOrEmpty(rel.Attributes?.Name)) return null;

            var person = new Person
            {
                ProviderId = provider.Id,
                SourceId = rel.Id,
                Name = rel.Attributes.Name,
                Type = rel.Type == "author" ? PersonRelationship.Author : PersonRelationship.Artist,
                Links = AuthorLinks(rel).ToArray(),
            };
            person.Id = await _db.People.Upsert(person);
            return person;
        }

        IEnumerable<ExternalLink> GroupLinks(ScanlationGroup rel)
        {
            Func<(string? value, string type)>[] items =
            [
                () => (rel.Attributes?.Website, "Website"),
                () => (rel.Attributes?.IrcServer, "IrcServer"),
                () => (rel.Attributes?.IrcChannel, "IrcChannel"),
                () => (rel.Attributes?.Discord, "Discord"),
                () => (rel.Attributes?.ContactEmail, "ContactEmail"),
                () => (rel.Attributes?.Twitter, "Twitter"),
                () => (rel.Attributes?.MangaUpdates, "MangaUpdates"),
            ];

            foreach (var item in items)
            {
                var (value, type) = item();
                if (string.IsNullOrEmpty(value)) continue;

                yield return new ExternalLink
                {
                    Platform = type,
                    Url = value,
                };
            }
        }

        async Task<Person?> Group(ScanlationGroup group)
        {
            if (string.IsNullOrEmpty(group.Attributes?.Name)) return null;

            var person = new Person
            {
                ProviderId = provider.Id,
                SourceId = group.Id,
                Name = group.Attributes.Name,
                Type = PersonRelationship.Group,
                Links = GroupLinks(group).ToArray(),
            };
            person.Id = await _db.People.Upsert(person);
            return person;
        }

        foreach (var rel in relationships)
        {
            switch(rel)
            {
                case PerRel person:
                    var author = await AuthorOrArtist(person);
                    if (author is not null)
                        yield return author;
                    break;
                case ScanlationGroup group:
                    var scanGroup = await Group(group);
                    if (scanGroup is not null)
                        yield return scanGroup;
                    break;

            }
        }
    }

    public async Task<(Series? series, string? error)> GetManga(string id, Provider provider)
    {
        MangaIncludes[] includes =
        [
            MangaIncludes.scanlation_group,
            MangaIncludes.cover_art,
            MangaIncludes.author,
            MangaIncludes.artist,
            MangaIncludes.tag,
            MangaIncludes.creator,
            MangaIncludes.user,
        ];
        var res = await _api.Manga.Get(id, includes);
        if (res.IsError(out var error))
            return (null, error);

        var manga = res.Data;
        var series = await GetSeries(manga, provider);
        if (series is null) return (null, "Failed to get series");

        var relationships = GetRelationships(manga.Relationships, provider);
        await foreach(var person in relationships)
            await _db.SeriesPeople.Upsert(new SeriesPeople 
            { 
                SeriesId = series.Id,
                PersonId = person.Id,
                Type = person.Type,
            });

        return (series, null);
    }
}
