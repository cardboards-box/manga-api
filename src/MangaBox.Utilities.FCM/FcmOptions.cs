namespace MangaBox.Utilities.FCM;

/// <summary>
/// The various options for configuration Firebase Cloud Messaging
/// </summary>
public class FcmOptions
{
	/// <summary>
	/// The optional path to the credentials file to use
	/// </summary>
	/// <remarks>Either this needs to be specified or the other options need to be set</remarks>
	[JsonIgnore]
	public string? CredentialFilePath { get; set; } = null;

	/// <summary>
	/// The scope to use for authentication
	/// </summary>
	[JsonIgnore]
	public string Scope { get; set; } = "https://www.googleapis.com/auth/firebase.messaging";

#pragma warning disable 1591
	//The following are a 1:1 mapping to the service account JSON file fields
	[JsonPropertyName("type")]
	public string Type { get; set; } = "service_account";

	[JsonPropertyName("project_id")]
	public string? ProjectId { get; set; } = null;

	[JsonPropertyName("private_key_id")]
	public string? PrivateKeyId { get; set; } = null;

	[JsonPropertyName("private_key")]
	public string? PrivateKey { get; set; } = null;

	[JsonPropertyName("client_email")]
	public string? ClientEmail { get; set; } = null;

	[JsonPropertyName("client_id")]
	public string? ClientId { get; set; } = null;

	[JsonPropertyName("client_x509_cert_url")]
	public string? ClientX509CertUrl { get; set; } = null;

	[JsonPropertyName("auth_uri")]
	public string AuthUri { get; set; } = "https://accounts.google.com/o/oauth2/auth";

	[JsonPropertyName("token_uri")]
	public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";

	[JsonPropertyName("auth_provider_x509_cert_url")]
	public string AuthProviderX509CertUrl { get; set; } = "https://www.googleapis.com/oauth2/v1/certs";

	[JsonPropertyName("universe_domain")]
	public string UniverseDomain { get; set; } = "googleapis.com";
#pragma warning restore 1591
}
