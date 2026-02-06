namespace MangaBox.Match.SauceNao;

/// <summary>
/// The data-sources for a sauce result
/// </summary>
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class SauceData
{
	[JsonPropertyName("ext_urls")]
	public string[] ExternalUrls { get; set; } = [];

	[JsonPropertyName("title")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Title { get; set; }

	[JsonPropertyName("author_name")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? AuthorName { get; set; }

	[JsonPropertyName("author_url")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? AuthorUrl { get; set; }

	[JsonPropertyName("pixiv_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? PixivId { get; set; }

	[JsonPropertyName("member_name")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? MemberName { get; set; }

	[JsonPropertyName("member_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? MemberId { get; set; }

	[JsonPropertyName("bcy_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? bcyId { get; set; }

	[JsonPropertyName("member_link_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? MemberLinkId { get; set; }

	[JsonPropertyName("bcy_type")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? BcyType { get; set; }

	[JsonPropertyName("created_at")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public DateTime? CreatedAt { get; set; }

	[JsonPropertyName("pawoo_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? PawooId { get; set; }

	[JsonPropertyName("pawoo_user_acct")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? PawooUserAccount { get; set; }

	[JsonPropertyName("pawoo_user_username")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? PawooUsername { get; set; }

	[JsonPropertyName("pawoo_user_display_name")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? PawooDisplayName { get; set; }

	[JsonPropertyName("anidb_aid")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? AnimeId { get; set; }

	[JsonPropertyName("source")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Source { get; set; }

	[JsonPropertyName("part")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Part { get; set; }

	[JsonPropertyName("year")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Year { get; set; }

	[JsonPropertyName("est_time")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? EstTime { get; set; }

	[JsonPropertyName("seiga_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? SeigaId { get; set; }

	[JsonPropertyName("sankaku_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? SankakuId { get; set; }

	[JsonPropertyName("danbooru_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? DanbooruId { get; set; }

	[JsonPropertyName("company")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Company { get; set; }

	[JsonPropertyName("getchu_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? GetchuId { get; set; }

	[JsonPropertyName("md_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? MangaDexId { get; set; }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
