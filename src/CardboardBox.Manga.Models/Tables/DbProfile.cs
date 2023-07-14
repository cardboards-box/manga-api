namespace CardboardBox.Manga.Models;

[Table("profiles")]
public class DbProfile : DbObject
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = "";

    [JsonPropertyName("platformId"), Column("platform_id", Unique = true)]
    public string PlatformId { get; set; } = "";

    [JsonPropertyName("admin")]
    public bool Admin { get; set; } = false;

    [JsonIgnore]
    public string Email { get; set; } = "";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("providerId"), Column("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("settingsBlob"), Column("settings_blob")]
    public string SettingsBlob { get; set; } = string.Empty;
}