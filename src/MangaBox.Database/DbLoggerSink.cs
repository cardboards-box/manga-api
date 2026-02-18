using Serilog.Core;
using Serilog.Events;
using System.Threading.Channels;

namespace MangaBox.Database;

using Models;
using Models.Types;

/// <summary>
/// The event sink for writing logs to the database
/// </summary>
/// <param name="_provider"></param>
public class DbLoggerSink(
	IFormatProvider? _provider = null) : ILogEventSink
{
	private static readonly Channel<MbLog> _queue = Channel.CreateUnbounded<MbLog>(new()
	{
		SingleReader = true,
		SingleWriter = true
	});

	/// <summary>
	/// The reader for the queue
	/// </summary>
	public static ChannelReader<MbLog> LogReader => _queue.Reader;

	internal static string ParseCategory(string message, out string? category)
	{
		category = null;
		if (!message.StartsWith('[')) return message;

		var end = message.IndexOf(']');
		if (end == -1) return message;

		category = message[..end].TrimStart('[').TrimEnd(']').Trim();
		return message[(end + 1)..].Trim();
	}

	/// <summary>
	/// Stops reading the queue
	/// </summary>
	public static void Finish()
	{
		_queue.Writer.Complete();
	}

	/// <inheritdoc />
	public void Emit(LogEvent logEvent)
	{
		var message = logEvent.RenderMessage(_provider);
		message = ParseCategory(message, out var category);

		var level = logEvent.Level switch
		{
			LogEventLevel.Verbose => MbLogLevel.Trace,
			LogEventLevel.Debug => MbLogLevel.Debug,
			LogEventLevel.Information => MbLogLevel.Information,
			LogEventLevel.Warning => MbLogLevel.Warning,
			LogEventLevel.Error => MbLogLevel.Error,
			LogEventLevel.Fatal => MbLogLevel.Critical,
			_ => MbLogLevel.None
		};


		var source = logEvent.Properties.TryGetValue("SourceContext", out var context)
			? context.ToString().Trim('\"') : null;

		var log = new MbLog
		{
			Category = category,
			Message = message,
			Exception = logEvent.Exception?.ToString(),
			LogLevel = level,
			Source = source,
		};
		_queue.Writer.WriteAsync(log).AsTask().Wait();
	}
}
