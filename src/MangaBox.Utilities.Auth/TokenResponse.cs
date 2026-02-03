namespace MangaBox.Utilities.Auth;

/// <summary>
/// The response from the token request
/// </summary>
/// <param name="Error">The error that occurred</param>
/// <param name="Provider">The OAuth provider</param>
/// <param name="User">The user's profile</param>
/// <param name="App">The application the user attempted to login to</param>
/// <param name="CreatedOn">The date and time the token was created</param>
public record class TokenResponse(
	[property: JsonPropertyName("error")] string? Error,
	[property: JsonPropertyName("provider")] string? Provider,
	[property: JsonPropertyName("user")] TokenUser? User,
	[property: JsonPropertyName("app")] TokenApp? App,
	[property: JsonPropertyName("createdOn")] DateTimeOffset CreatedOn);

/// <summary>
/// The application the user attempted to login to
/// </summary>
/// <param name="Name">The name of the application</param>
/// <param name="Icon">The icon of the application</param>
/// <param name="Background">The background of the application</param>
public record class TokenApp(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("icon")] string Icon,
	[property: JsonPropertyName("background")] string Background);

/// <summary>
/// The user's profile
/// </summary>
/// <param name="Id">The Unique ID of the user on the OAuth platform</param>
/// <param name="Nickname">The nickname of the user</param>
/// <param name="Avatar">The avatar of the user</param>
/// <param name="Email">The email of the user</param>
/// <param name="Provider">The OAuth provider</param>
/// <param name="ProviderId">The OAuth provider-specific ID of the user</param>
public record class TokenUser(
	[property: JsonPropertyName("id")] string Id,
	[property: JsonPropertyName("nickname")] string Nickname,
	[property: JsonPropertyName("avatar")] string Avatar,
	[property: JsonPropertyName("email")] string Email,
	[property: JsonPropertyName("provider")] string Provider,
	[property: JsonPropertyName("providerId")] string ProviderId);