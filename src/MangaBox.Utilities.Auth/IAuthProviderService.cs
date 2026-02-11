namespace MangaBox.Utilities.Auth;

using Models;

/// <summary>
/// An service for interacting with an OAuth provider
/// </summary>
public interface IAuthProviderService
{
	/// <summary>
	/// The name of the provider
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Builds the authentication URL for the provider
	/// </summary>
	/// <param name="stateId">The ID of the state data</param>
	/// <param name="callBackUrl">The callback URL</param>
	/// <returns>The authentication URL</returns>
	string AuthUrl(Guid stateId, string callBackUrl);

	/// <summary>
	/// Exchanges the challenge code for an access token with the provider
	/// </summary>
	/// <param name="code">The challenge code</param>
	/// <param name="callBackUrl">The callback URL</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The access token</returns>
	Task<string> GetAccessToken(string code, string callBackUrl, CancellationToken token);

	/// <summary>
	/// Fetches the user's profile information from the provider using the access token
	/// </summary>
	/// <param name="accessToken">The provider's access token</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The user's partial profile</returns>
	Task<MbProfile.ProfilePartial> GetUser(string accessToken, CancellationToken token);
}
