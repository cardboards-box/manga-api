namespace MangaBox.Models.Types;

/// <summary>
/// The various levels of logs
/// </summary>
public enum MbLogLevel
{
	/// <summary>
	/// Logs that contain the most detailed messages. These messages may contain sensitive application data.
	/// These messages are disabled by default and should never be enabled in a production environment.
	/// </summary>
	[Display(Name = "Trace")]
	[Description("Logs that contain the most detailed messages")]
	Trace = 0,

	/// <summary>
	/// Logs that are used for interactive investigation during development.  These logs should primarily contain
	/// information useful for debugging and have no long-term value.
	/// </summary>
	[Display(Name = "Debug")]
	[Description("Logs that are used for interactive investigation during development")]
	Debug = 1,

	/// <summary>
	/// Logs that track the general flow of the application. These logs should have long-term value.
	/// </summary>
	[Display(Name = "Information")]
	[Description("Logs that track the general flow of the application")]
	Information = 2,

	/// <summary>
	/// Logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the
	/// application execution to stop.
	/// </summary>
	[Display(Name = "Warning")]
	[Description("Logs that highlight an abnormal or unexpected event in the application flow")]
	Warning = 3,

	/// <summary>
	/// Logs that highlight when the current flow of execution is stopped due to a failure. These should indicate a
	/// failure in the current activity, not an application-wide failure.
	/// </summary>
	[Display(Name = "Error")]
	[Description("Logs that highlight when the current flow of execution is stopped due to a failure")]
	Error = 4,

	/// <summary>
	/// Logs that describe an unrecoverable application or system crash, or a catastrophic failure that requires
	/// immediate attention.
	/// </summary>
	[Display(Name = "Critical")]
	[Description("Logs that describe an unrecoverable application or system crash, or a catastrophic failure that requires immediate attention")]
	Critical = 5,

	/// <summary>
	/// Not used for writing log messages. Specifies that a logging category should not write any messages.
	/// </summary>
	[Display(Name = "None")]
	[Description("Not used for writing log messages. Specifies that a logging category should not write any messages")]
	None = 6,
}
