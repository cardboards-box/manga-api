using System.Threading.Channels;

namespace MangaBox.Core;

/// <summary>
/// Extension methods
/// </summary>
public static class Extensions
{
	/// <summary>
	/// Checks the validity of a POCO
	/// </summary>
	/// <typeparam name="T">The type of POCO</typeparam>
	/// <param name="value">The value of the POCO</param>
	/// <param name="errors">Any validation errors</param>
	/// <returns>Whether or not the POCO is valid</returns>
	public static bool IsValid<T>(this T value, out string[] errors)
		where T : IValidator
	{
		var context = new ValidationContext(value);
		var results = new List<ValidationResult>();
		var isValid = Validator.TryValidateObject(value, context, results, true);
		errors = results
			.Select(r => r.ErrorMessage)
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.Select(t => t!)
			.ToArray();
		return isValid;
	}

	/// <summary>
	/// Checks the validity of a collection of POCO
	/// </summary>
	/// <typeparam name="T">The type of POCO</typeparam>
	/// <param name="values">The values of the POCO</param>
	/// <param name="errors">Any validation errors</param>
	/// <returns>Whether or not the POCOs are valid</returns>
	public static bool IsValid<T>(this IEnumerable<T> values, out string[] errors)
		where T : IValidator
	{
		var results = new List<string>();
		foreach (var value in values)
		{
			if (!value.IsValid(out var valueErrors))
				results.AddRange(valueErrors);
		}
		errors = [.. results];
		return results.Count == 0;
	}

	private readonly static ConcurrentDictionary<Type, EnumDescription[]> _enumDescriptionCache = [];

	/// <summary>
	/// Get all of the values of the given enum type
	/// </summary>
	/// <param name="type">The type of enum</param>
	/// <returns>All of the values of the enum</returns>
	/// <exception cref="ArgumentException">Thrown if the given type isn't an enum</exception>
	public static IEnumerable<Enum> AllFlags(this Type type)
	{
		if (!type.IsEnum)
			throw new ArgumentException("Type must be an enum", nameof(type));
		var values = Enum.GetValues(type);
		foreach (var value in values)
		{
			if (value is Enum enumValue)
				yield return enumValue;
		}
	}

	/// <summary>
	/// Gets an attribute on an enum field value
	/// </summary>
	/// <typeparam name="T">The type of the attribute you want to retrieve</typeparam>
	/// <param name="enumVal">The enum value</param>
	/// <returns>The attribute of type T that exists on the enum value</returns>
	/// <example><![CDATA[string desc = myEnumVariable.GetAttributeOfType<DescriptionAttribute>().Description;]]></example>
	public static T? GetAttributeOfType<T>(this Enum enumVal) where T : Attribute
	{
		var type = enumVal.GetType();
		var memInfo = type.GetMember(enumVal.ToString());
		var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
		return (attributes.Length > 0) ? (T)attributes[0] : null;
	}

	/// <summary>
	/// Generates a list of all of the enum descriptions
	/// </summary>
	/// <param name="type">The type of enum</param>
	/// <param name="skipZero">Whether or not to skip enums with a value of 0</param>
	/// <param name="onlyBits">Whether or not to only include base 2 values</param>
	/// <returns>All of the enum descriptions</returns>
	public static EnumDescription[] Describe(this Type type, bool skipZero = true, bool onlyBits = false)
	{
		if (!type.IsEnum)
			throw new ArgumentException("Type must be an enum", nameof(type));

		if (!_enumDescriptionCache.TryGetValue(type, out var descriptions))
			descriptions = _enumDescriptionCache[type] = [
				..type.AllFlags()
					.Select(t =>
					{
						var display = t.GetAttributeOfType<DisplayAttribute>();
						var name = display?.Name?.ForceNull() ?? t.ToString();
						var value = (long)Convert.ChangeType(t, typeof(long));
						var description = t.GetAttributeOfType<DescriptionAttribute>()?.Description;
						return new EnumDescription(name, description, value, t.ToString());
					})
					.OrderBy(t => t.Value)
			];

		if (!skipZero && !onlyBits) return descriptions;

		IEnumerable<EnumDescription> flags = descriptions;

		if (skipZero)
			flags = flags.Where(t => t.Value != 0);
		if (onlyBits)
			flags = flags.Where(t => (t.Value & (t.Value - 1)) == 0);

		return [..flags];
	}

	/// <summary>
	/// Generates a list of all of the enum descriptions
	/// </summary>
	/// <typeparam name="T">The type of enum</typeparam>
	/// <param name="enum">A dummy value to get the flags from</param>
	/// <param name="skipZero">Whether or not to skip enums with a value of 0</param>
	/// <param name="onlyBits">Whether or not to only include base 2 values</param>
	/// <returns>All of the enum descriptions</returns>
	public static EnumDescription[] Describe<T>(this T @enum, bool skipZero = true, bool onlyBits = false)
		where T : Enum
	{
		var type = typeof(T);
		return Describe(type, skipZero, onlyBits);
	}

	/// <summary>
	/// Attempt to find the file at the given path in various root directories
	/// </summary>
	/// <param name="path">The path to find</param>
	/// <returns>The localized file path (or null if not found)</returns>
	public static string? FindFile(this string path)
	{
		if (File.Exists(path)) return path;

		var roots = new[]
		{
			AppDomain.CurrentDomain.BaseDirectory,
			AppDomain.CurrentDomain.RelativeSearchPath,
			AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
			"./"
		};

		foreach (var root in roots)
		{
			if (string.IsNullOrEmpty(root)) continue;

			var fullPath = Path.Combine(root, path);
			if (File.Exists(fullPath))
				return fullPath;
		}

		return null;
	}

	/// <summary>
	/// Iterates through the collection in parallel
	/// </summary>
	/// <typeparam name="TResult">The type of the results</typeparam>
	/// <typeparam name="TSource">The type of the data</typeparam>
	/// <param name="source">The data source</param>
	/// <param name="action">The action to do on each item</param>
	/// <param name="parallels">The maximum degree of parallelism</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>An async enumerable of results</returns>
	public static IAsyncEnumerable<TResult> ParallelForeach<TResult, TSource>(
		this IAsyncEnumerable<TSource> source,
		Func<TSource, CancellationToken, ValueTask<TResult>> action,
		int? parallels = null,
		CancellationToken token = default)
	{
		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = parallels ?? Environment.ProcessorCount,
			CancellationToken = token
		};

		var channel = Channel.CreateUnbounded<TResult>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});

		Parallel.ForEachAsync(source, opts, async (item, ct) =>
		{
			var result = await action(item, ct).ConfigureAwait(false);
			await channel.Writer.WriteAsync(result, ct).ConfigureAwait(false);
		}).ContinueWith(t => channel.Writer.Complete(t.Exception), token);

		return channel.Reader.ReadAllAsync(token);
	}

	/// <summary>
	/// Iterates through the collection in parallel
	/// </summary>
	/// <typeparam name="TResult">The type of the results</typeparam>
	/// <typeparam name="TSource">The type of the data</typeparam>
	/// <param name="source">The data source</param>
	/// <param name="action">The action to do on each item</param>
	/// <param name="parallels">The maximum degree of parallelism</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>An async enumerable of results</returns>
	public static IAsyncEnumerable<TResult> ParallelForeach<TResult, TSource>(
		this IEnumerable<TSource> source, 
		Func<TSource, CancellationToken, ValueTask<TResult>> action,
		int? parallels = null,
		CancellationToken token = default)
	{
		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = parallels ?? Environment.ProcessorCount,
			CancellationToken = token
		};

		var channel = Channel.CreateUnbounded<TResult>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});

		Parallel.ForEachAsync(source, opts, async (item, ct) =>
		{
			var result = await action(item, ct).ConfigureAwait(false);
			await channel.Writer.WriteAsync(result, ct).ConfigureAwait(false);
		}).ContinueWith(t =>
		{
			channel.Writer.Complete(t.Exception);
		}, token);

		return channel.Reader.ReadAllAsync(token);
	}

	/// <summary>
	/// Because dotnet 10 is fucking stupid and has an ambiguous reference to ToListAsync
	/// </summary>
	/// <typeparam name="T">The type of the elements in the async enumerable</typeparam>
	/// <param name="source">The source collection</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The outbound collection</returns>
	public static async ValueTask<List<T>> ToList<T>(this IAsyncEnumerable<T> source, CancellationToken token)
	{
		List<T> items = [];
		await foreach (var item in source
			.WithCancellation(token)
			.ConfigureAwait(false))
			items.Add(item);
		return items;
	}
}
