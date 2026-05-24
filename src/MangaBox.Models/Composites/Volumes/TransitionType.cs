namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// Indicates the impact of the transition to the next chapter.
/// </summary>
public enum TransitionType
{
	/// <summary>
	/// The next chapter is in the same partial set
	/// </summary>
	Partial = 0,
	/// <summary>
	/// The next chapter in the same volume
	/// </summary>
	Chapter = 1,
	/// <summary>
	/// The next chapter is in a different volume
	/// </summary>
	Volume = 2,
	/// <summary>
	/// There is no next chapter in the manga
	/// </summary>
	End = 3,
}
