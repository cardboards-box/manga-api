namespace MangaBox.Models.Composites;

/// <summary>
/// The response to an auth request
/// </summary>
/// <param name="Token">The JWT token</param>
/// <param name="Profile">The user's profile</param>
public record class AuthResponse(
	[property: JsonPropertyName("token")] string Token,
	[property: JsonPropertyName("profile")] MbProfile Profile);