namespace MangaBox.Utilities.Auth;

/// <summary>
/// Represents a request to the authentication platform
/// </summary>
/// <param name="Code">The login code for the authentication platform</param>
/// <param name="Secret">The secret key for the authentication platform</param>
/// <param name="AppId">The application ID for the authentication platform</param>
public record class TokenRequest(
	[property: JsonPropertyName("Code")] string Code,
	[property: JsonPropertyName("Secret")] string Secret,
	[property: JsonPropertyName("AppId")] string AppId);
