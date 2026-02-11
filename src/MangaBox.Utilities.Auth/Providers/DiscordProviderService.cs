using System.Net.Http.Headers;

namespace MangaBox.Utilities.Auth.Providers;

using Models;

internal class DiscordProviderService(
	IOptions<AuthOptions> _config,
	IHttpClientFactory _factory) : IAuthProviderService
{
	public string Name => "discord";

	public string ClientId => _config.Value.DiscordClientId;
	public string ClientSecret => _config.Value.DiscordClientSecret;

	public string AuthUrl(Guid stateId, string callBackUrl)
	{
		var scope = Uri.EscapeDataString("identify email");
		return $"https://discord.com/oauth2/authorize" +
			   $"?client_id={Uri.EscapeDataString(ClientId)}" +
			   $"&redirect_uri={Uri.EscapeDataString(callBackUrl)}" +
			   $"&response_type=code" +
			   $"&scope={scope}" +
			   $"&state={stateId}";
	}

	public static string AvatarUrl(DiscordUser user)
	{
		var ext = user.Avatar.StartsWith("a_") ? "gif" : "png";
		return $"https://cdn.discordapp.com/avatars/{user.Id}/{user.Avatar}.{ext}?size=512";
	}

	public async Task<string> GetAccessToken(string code, string callBackUrl, CancellationToken token)
	{
		using var client = _factory.CreateClient();

		using var content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["client_id"] = ClientId,
			["client_secret"] = ClientSecret,
			["grant_type"] = "authorization_code",
			["code"] = code,
			["redirect_uri"] = callBackUrl,
		});
		using var resp = await client.PostAsync("https://discord.com/api/v9/oauth2/token", content, token);
		resp.EnsureSuccessStatusCode();

		using var stream = await resp.Content.ReadAsStreamAsync(token);
		var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);

		var access = doc.RootElement.GetProperty("access_token").GetString();
		return access ?? throw new InvalidOperationException("Discord access_token missing");
	}

	public async Task<MbProfile.ProfilePartial> GetUser(string accessToken, CancellationToken token)
	{
		using var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

		using var resp = await client.GetAsync("https://discord.com/api/v9/users/@me", token);
		resp.EnsureSuccessStatusCode();
		using var stream = await resp.Content.ReadAsStreamAsync(token);
		var user = await JsonSerializer.DeserializeAsync<DiscordUser>(stream, cancellationToken: token)
			?? throw new InvalidOperationException("Failed to deserialize Discord user");
		return new(Name, user.Id, user.Email, user.Username, AvatarUrl(user));
	}

	public class DiscordUser
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("username")]
		public string Username { get; set; } = string.Empty;

		[JsonPropertyName("avatar")]
		public string Avatar { get; set; } = string.Empty;

		[JsonPropertyName("email")]
		public string Email { get; set; } = string.Empty;

		[JsonPropertyName("verified")]
		public bool Verified { get; set; }
	}
}
