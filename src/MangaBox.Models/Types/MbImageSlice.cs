namespace MangaBox.Models.Types;

/// <summary>
/// Represents a slice of an image
/// </summary>
[Type("mb_image_slice")]
public class MbImageSlice : IValidator, IDbType
{
	/// <summary>
	/// The ID of the <see cref="MbImage"/> this slice is from
	/// </summary>
	[Required]
	[JsonPropertyName("image")]
	public Guid Image { get; set; }

	/// <summary>
	/// The ordinal of the slice in the output image
	/// </summary>
	[Required]
	[JsonPropertyName("ordinal")]
	public int Ordinal { get; set; }

	/// <summary>
	/// The start Y coordinate of the slice in the image
	/// </summary>
	[Required]
	[JsonPropertyName("start")]
	public int Start { get; set; }

	/// <summary>
	/// The end Y coordinate of the slice in the image
	/// </summary>
	[Required]
	[JsonPropertyName("stop")]
	public int Stop { get; set; }
}
