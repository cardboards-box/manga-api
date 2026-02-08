namespace MangaBox.Utilities.Auth;

internal class OAuthOptions
{
	public string AppId { get; set; } = string.Empty;

	public string Secret { get; set; } = string.Empty;

	public string Url { get; set; } = "https://auth.index-0.com";

	public string[] ReturnUrls { get; set; } = ["https://localhost:7115/resolve"];
}
