namespace CardboardBox.Manga.Models.Requests.Filtering;

public class MangaFilter
{
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("size")]
    public int Size { get; set; } = 100;

    [JsonPropertyName("search")]
    public string? Search { get; set; }

    [JsonPropertyName("asc")]
    public bool Ascending { get; set; } = true;

    [JsonPropertyName("include")]
    public string[] Include { get; set; } = Array.Empty<string>();

    [JsonPropertyName("exclude")]
    public string[] Exclude { get; set; } = Array.Empty<string>();

    [JsonPropertyName("sources")]
    public string[] Sources { get; set; } = Array.Empty<string>();

    [JsonPropertyName("sort")]
    public int? Sort { get; set; }

    [JsonPropertyName("state")]
    public TouchedState State { get; set; } = TouchedState.All;

    [JsonPropertyName("nsfw")]
    public NsfwCheck Nsfw { get; set; } = NsfwCheck.Sfw;

    [JsonPropertyName("attributes")]
    public MangaAttributeFilter[] Attributes { get; set; } = Array.Empty<MangaAttributeFilter>();
}
