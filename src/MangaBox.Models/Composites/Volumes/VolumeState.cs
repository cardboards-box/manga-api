namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// Represents the state of the volume
/// </summary>
public enum VolumeState
{
	/// <summary>
	/// The user hasn't started reading the volume
	/// </summary>
	[Display(Name = "Not Started")]
	[Description("The you haven't started reading the volume")]
	NotStarted,
	/// <summary>
	/// The user has started reading the volume but hasn't completed it
	/// </summary>
	[Display(Name = "In Progress")]
	[Description("You have started reading the volume but haven't completed it")]
	InProgress,
	/// <summary>
	/// The user has completely read the volume
	/// </summary>
	[Display(Name = "Completed")]
	[Description("You have completely read the volume")]
	Completed
}
