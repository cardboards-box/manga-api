namespace MangaBox.Utilities.Auth;

internal class AuthOptions
{
	public double TTLMinutes { get; set; } = 5;

	public string AppUrl { get; set; } = string.Empty;

	public string[] ReturnUrls { get; set; } = ["https://localhost:7115/resolve"];

	public string DiscordClientId { get; set; } = string.Empty;

	public string DiscordClientSecret { get; set; } = string.Empty;
}
