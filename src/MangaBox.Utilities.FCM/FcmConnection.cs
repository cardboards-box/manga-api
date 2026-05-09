using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace MangaBox.Utilities.FCM;

internal class FcmConnection(
	IOptions<FcmOptions> _options)
{
	private FirebaseMessaging? _messaging = null;

	public FirebaseMessaging Instance => _messaging ??= Initialize();

	public const string CredentialType = JsonCredentialParameters.ServiceAccountCredentialType;

	public GoogleCredential Credentials()
	{
		//Load the credentials from a file if specified
		if (!string.IsNullOrWhiteSpace(_options.Value.CredentialFilePath))
			return CredentialFactory.FromFile(_options.Value.CredentialFilePath, CredentialType);

		//Convert the options to JSON and load from that
		var json = JsonSerializer.Serialize(_options.Value);
		return CredentialFactory.FromJson(json, CredentialType);
	}

	public FirebaseMessaging Initialize()
	{
		var creds = Credentials().CreateScoped(_options.Value.Scope);
		var options = new AppOptions
		{
			Credential = creds
		};
		var app = FirebaseApp.Create(options);
		return FirebaseMessaging.GetMessaging(app);
	}
}
