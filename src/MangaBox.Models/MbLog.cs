namespace MangaBox.Models;

using Types;

/// <summary>
/// A table for logging various events in the application
/// </summary>
[Table("mb_logs")]
[InterfaceOption(nameof(MbLog))]
[Searchable(nameof(Category), nameof(Source), nameof(Message), nameof(Exception), nameof(Context))]
public class MbLog : MbDbObject
{
	/// <summary>
	/// The level of the log
	/// </summary>
	[Column("log_level")]
	[JsonPropertyName("logLevel")]
	public MbLogLevel LogLevel { get; set; }

	/// <summary>
	/// The category the log belongs to
	/// </summary>
	[Column("category")]
	[JsonPropertyName("category")]
	public string? Category { get; set; }

	/// <summary>
	/// The context source for the event
	/// </summary>
	[Column("source")]
	[JsonPropertyName("source")]
	public string? Source { get; set; }

	/// <summary>
	/// The message of the log
	/// </summary>
	[Column("message")]
	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// The exception message if one is present
	/// </summary>
	[Column("exception")]
	[JsonPropertyName("exception")]
	public string? Exception { get; set; }

	/// <summary>
	/// The context of the log
	/// </summary>
	[Column("context")]
	[JsonPropertyName("context")]
	public string? Context { get; set; }
}
