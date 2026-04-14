namespace MangaBox.Models;

/// <summary>
/// Represents an API key for authenticating with the API
/// </summary>
[Table("mb_api_keys")]
[InterfaceOption(nameof(MbApiKey))]
public class MbApiKey : MbDbObject
{
    /// <summary>
    /// The ID of the profile this API key belongs to
    /// </summary>
    [Column("profile_id", Unique = true), Fk<MbProfile>]
    [JsonPropertyName("profileId")]
    [Required]
    public Guid ProfileId { get; set; }

    /// <summary>
    /// The name of the API key
    /// </summary>
    [Column("name", Unique = true)]
    [JsonPropertyName("name")]
    [Required, MinLength(3), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The value of the API key
    /// </summary>
    [Column("key")]
    [JsonIgnore]
    [Required]
    public string Key { get; set; } = string.Empty;
}
