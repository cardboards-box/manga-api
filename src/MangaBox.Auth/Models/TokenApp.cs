namespace MangaBox.Auth;

public class TokenApp
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("background")]
    public string Background { get; set; } = string.Empty;
}
