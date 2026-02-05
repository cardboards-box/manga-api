using System.Xml;
using System.Xml.Serialization;

namespace MangaBox.Services.CBZModels;

/// <summary>
/// Helpers for converting ComicInfo "Yes/No" and page type strings.
/// </summary>
internal static class ComicInfoXmlHelpers
{
	/// <summary>
	/// Parses common ComicInfo boolean encodings (Yes/No, True/False, 1/0).
	/// Returns null for unknown/blank values.
	/// </summary>
	public static bool? StringToBool(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return value.Trim().ToLowerInvariant() switch
		{
			"yes" or "true" or "1" => true,
			"no" or "false" or "0" => false,
			_ => null
		};
	}

	/// <summary>
	/// Converts a nullable bool into "Yes"/"No" (null => null).
	/// </summary>
	public static string? BoolToString(bool? value)
		=> value is null ? null : (value.Value ? "Yes" : "No");

	/// <summary>
	/// Serializes an object to XML and writes it to the provided stream.
	/// </summary>
	/// <typeparam name="T">Type of object to serialize.</typeparam>
	/// <param name="value">The object instance to serialize.</param>
	/// <param name="output">Destination stream.</param>
	/// <param name="omitXmlDeclaration">If true, omits the XML declaration.</param>
	public static void SerializeToStream<T>(
		T value,
		Stream output,
		bool omitXmlDeclaration = false)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(output);

		var serializer = new XmlSerializer(typeof(T));

		var settings = new XmlWriterSettings
		{
			Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			Indent = true,
			OmitXmlDeclaration = omitXmlDeclaration,
			NewLineHandling = NewLineHandling.Entitize
		};

		// Prevent xmlns:xsi / xmlns:xsd noise
		var ns = new XmlSerializerNamespaces();
		ns.Add(string.Empty, string.Empty);

		using var writer = XmlWriter.Create(output, settings);
		serializer.Serialize(writer, value, ns);
	}
}
