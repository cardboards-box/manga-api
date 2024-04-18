namespace MangaBox.Auth;

public class TokenResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("user")]
    public TokenUser User { get; set; } = new();

    [JsonPropertyName("app")]
    public TokenApp App { get; set; } = new();

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("createdOn")]
    public DateTimeOffset CreatedOn { get; set; }
}
