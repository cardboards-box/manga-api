namespace CardboardBox.Manga.ReverseImageSearch;

using MatchApi;
using Models;

public class ImageSearchResults
{
    [JsonPropertyName("vision")]
    public List<VisionResult> Vision { get; set; } = new();

    [JsonPropertyName("match")]
    public List<FallbackResult> Match { get; set; } = new();

    [JsonPropertyName("textual")]
    public List<BaseResult> Textual { get; set; } = new();

    [JsonPropertyName("bestGuess")]
    public TrimmedManga? BestGuess { get; set; }

    [JsonIgnore]
    public bool Success => Vision.Count > 0 || Match.Count > 0 || Textual.Count > 0;

    [JsonIgnore]
    public IEnumerable<BaseResult> All => Match.Concat<BaseResult>(Vision).Concat(Textual);

    public class TrimmedManga
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("nsfw")]
        public bool Nsfw { get; set; }

        [JsonPropertyName("cover")]
        public string Cover { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();

        public static implicit operator TrimmedManga(DbManga manga)
        {
            return new TrimmedManga
            {
                Title = manga.Title,
                Id = manga.SourceId,
                Url = $"https://mangadex.org/title/" + manga.SourceId,
                Description = manga.Description,
                Tags = manga.Tags,
                Cover = manga.Cover,
                Nsfw = manga.Nsfw,
                Source = manga.Provider
            };
        }
    }

    public class BaseResult
    {
        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("exactMatch")]
        public bool ExactMatch { get; set; }

        [JsonPropertyName("source")]
        public virtual string Source { get; set; } = string.Empty;

        [JsonPropertyName("manga")]
        public TrimmedManga? Manga { get; set; }
    }

    public class VisionResult : BaseResult
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("filteredTitle")]
        public string FilteredTitle { get; set; } = string.Empty;

        public override string Source { get; set; } = "google vision";
    }

    public class FallbackResult : BaseResult
    {
        [JsonPropertyName("metadata")]
        public MangaMetadata? Metadata { get; set; }

        public override string Source { get; set; } = "cba fallback";
    }
}
