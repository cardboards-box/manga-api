namespace MangaBox.Utilities.FCM;

/// <summary>
/// Represents localized data in a notification
/// </summary>
/// <param name="Key">The localization key to use</param>
/// <param name="Arguments">The arguments to use for the localization key</param>
public record class Localization(
	string Key,
	string[] Arguments);