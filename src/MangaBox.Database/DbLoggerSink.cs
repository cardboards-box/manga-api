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

#if DEBUG
	internal static bool DEBUG = true;
#else
	internal static bool DEBUG = false;
#endif

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false
	};

	private static readonly Channel<MbLog> _queue = Channel.CreateUnbounded<MbLog>(new()
	{
		SingleReader = true,
		SingleWriter = true
	});

	/// <summary>
	/// The reader for the queue
	/// </summary>
	public static ChannelReader<MbLog> LogReader => _queue.Reader;

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

		var source = logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
			? GetScalarString(sourceContext)
			: null;

		var context = SerializeContext(logEvent);

		var log = new MbLog
		{
			Category = category,
			Message = message,
			Exception = logEvent.Exception?.ToString(),
			LogLevel = level,
			Source = source,
			Context = context
		};
		_queue.Writer.TryWrite(log);
	}

	internal static string ParseCategory(string message, out string? category)
	{
		category = null;
		if (!message.StartsWith('[')) return message;

		var end = message.IndexOf(']');
		if (end == -1) return message;

		category = message[..end].TrimStart('[').TrimEnd(']').Trim();
		return message[(end + 1)..].Trim();
	}

	internal static string? SerializeContext(LogEvent logEvent)
	{
		try
		{
			if (logEvent.Properties.Count == 0)
				return null;

			var context = new Dictionary<string, object?>();

			foreach (var property in logEvent.Properties)
			{
				if (property.Key == "SourceContext")
					continue;

				context[property.Key] = ConvertPropertyValue(property.Value);
			}

			return context.Count == 0
				? null
				: JsonSerializer.Serialize(context, JsonOptions);
		}
		catch
		{
			//Only throw in debug mode, otherwise void the error
			if (DEBUG)
				throw;
			return null;
		}
	}

	internal static object? ConvertPropertyValue(LogEventPropertyValue value)
	{
		return value switch
		{
			ScalarValue scalar => scalar.Value,
			SequenceValue sequence => sequence.Elements.Select(ConvertPropertyValue).ToList(),
			StructureValue structure => structure.Properties.ToDictionary(
				x => x.Name,
				x => ConvertPropertyValue(x.Value)),
			DictionaryValue dictionary => dictionary.Elements.ToDictionary(
				x => GetDictionaryKey(x.Key),
				x => ConvertPropertyValue(x.Value)),
			_ => value.ToString()
		};
	}

	internal static string GetDictionaryKey(ScalarValue key)
	{
		return key.Value?.ToString() ?? string.Empty;
	}

	internal static string? GetScalarString(LogEventPropertyValue value)
	{
		return value is ScalarValue scalar
			? scalar.Value?.ToString()
			: value.ToString().Trim('"');
	}
}
