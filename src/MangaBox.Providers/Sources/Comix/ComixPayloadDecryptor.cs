using System.Net;
using System.Text;
using System.Text.Json;

namespace MangaBox.Providers.Sources.Comix;

internal static class ComixPayloadDecryptor
{
	private static readonly byte[][] Rc4Keys =
	[
		Convert.FromBase64String("13YDu67uDgFczo3DnuTIURqas4lfMEPADY6Jaeqky+w="),
		Convert.FromBase64String("vZ23RT7pbSlxwiygkHd1dhToIku8SNHPC6V36L4cnwM="),
		Convert.FromBase64String("BkWI8feqSlDZKMq6awfzWlUypl88nz65KVRmpH0RWIc="),
		Convert.FromBase64String("RougjiFHkSKs20DZ6BWXiWwQUGZXtseZIyQWKz5eG34="),
		Convert.FromBase64String("U9LRYFL2zXU4TtALIYDj+lCATRk/EJtH7/y7qYYNlh8="),
	];

	private static readonly byte[][] XorKeys =
	[
		Convert.FromBase64String("yEy7wBfBc+gsYPiQL/4Dfd0pIBZFzMwrtlRQGwMXy3Q="),
		Convert.FromBase64String("QX0sLahOByWLcWGnv6l98vQudWqdRI3DOXBdit9bxCE="),
		Convert.FromBase64String("v7EIpiQQjd2BGuJzMbBA0qPWDSS+wTJRQ7uGzZ6rJKs="),
		Convert.FromBase64String("LL97cwoDoG5cw8QmhI+KSWzfW+8VehIh+inTxnVJ2ps="),
		Convert.FromBase64String("e/GtffFDTvnw7LBRixAD+iGixjqTq9kIZ1m0Hj+s6fY="),
	];

	private static readonly int[] PrependCounts = [7, 6, 7, 8, 6];

	public static bool TryDecryptPayload(string encryptedPayload, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(encryptedPayload))
			return false;

		byte[] bytes;
		try
		{
			bytes = DecodeBase64Url(encryptedPayload);
		}
		catch
		{
			return false;
		}

		if (TryNormalizeDecodedBytes(bytes, out json))
			return true;

		if (TryDecodeNestedBase64Layer(bytes, out var nestedInitial) && TryNormalizeDecodedBytes(nestedInitial, out json))
			return true;

		for (var round = Rc4Keys.Length - 1; round >= 0; round--)
		{
			bytes = ReverseRound(round, bytes);
			if (bytes.Length == 0)
				return false;

			if (TryNormalizeDecodedBytes(bytes, out json))
				return true;

			if (TryDecodeNestedBase64Layer(bytes, out var nestedRound) && TryNormalizeDecodedBytes(nestedRound, out json))
				return true;
		}

		return false;
	}

	private static byte[] ReverseRound(int round, byte[] output)
	{
		var prependCount = PrependCounts[round];
		if (output.Length < prependCount)
			return [];

		var inputLength = output.Length - prependCount;
		if (inputLength <= 0)
			return [];

		var rc4Output = new byte[inputLength];
		var outIndex = 0;
		for (var i = 0; i < inputLength; i++)
		{
			if (i < prependCount)
				outIndex++;

			if (outIndex >= output.Length)
				return [];

			var transformed = output[outIndex++];
			var untransformed = ReverseTransform(round, i % 10, transformed);
			rc4Output[i] = (byte)(untransformed ^ XorKeys[round][i % 32]);
		}

		return Rc4(Rc4Keys[round], rc4Output);
	}

	private static byte ReverseTransform(int round, int pos, byte value)
	{
		return round switch
		{
			0 => pos switch
			{
				0 or 9 => Subtract(value, 115),
				1 => Add(value, 12),
				2 or 8 => RotateRight1(value),
				3 => RotateLeft4(value),
				4 or 6 => Add(value, 42),
				5 => Subtract(value, 143),
				7 => Subtract(value, 15),
				_ => value,
			},
			1 => pos switch
			{
				0 or 8 => Subtract(value, 115),
				1 => Add(value, 12),
				2 or 6 => RotateLeft4(value),
				3 => Add(value, 42),
				4 or 9 => Subtract(value, 143),
				5 => Subtract(value, 15),
				7 => Add(value, 20),
				_ => value,
			},
			2 => pos switch
			{
				0 => Subtract(value, 115),
				1 => Add(value, 188),
				2 or 8 => Subtract(value, 143),
				3 => RotateRight2(value),
				4 => RotateRight1(value),
				5 => Xor(value, 177),
				6 => RotateLeft4(value),
				7 => Subtract(value, 15),
				9 => Add(value, 12),
				_ => value,
			},
			3 => pos switch
			{
				0 => Add(value, 12),
				1 or 9 => Xor(value, 177),
				2 or 7 or 8 => RotateRight1(value),
				3 or 5 => Subtract(value, 143),
				4 or 6 => Add(value, 20),
				_ => value,
			},
			4 => pos switch
			{
				0 => Add(value, 20),
				1 or 7 => Subtract(value, 143),
				2 => Subtract(value, 115),
				3 or 5 => Xor(value, 177),
				4 => Add(value, 12),
				6 => Add(value, 188),
				8 => RotateLeft4(value),
				9 => RotateRight2(value),
				_ => value,
			},
			_ => value,
		};
	}

	private static byte[] DecodeBase64Url(string value)
	{
		var padded = value.Replace('-', '+').Replace('_', '/');
		var remainder = padded.Length % 4;
		if (remainder != 0)
			padded += new string('=', 4 - remainder);

		return Convert.FromBase64String(padded);
	}

	private static byte[] Rc4(byte[] key, byte[] data)
	{
		var state = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
		var j = 0;
		for (var i = 0; i < 256; i++)
		{
			j = (j + state[i] + key[i % key.Length]) & 255;
			(state[i], state[j]) = (state[j], state[i]);
		}

		var output = new byte[data.Length];
		var x = 0;
		var y = 0;
		for (var i = 0; i < data.Length; i++)
		{
			x = (x + 1) & 255;
			y = (y + state[x]) & 255;
			(state[x], state[y]) = (state[y], state[x]);
			output[i] = (byte)(data[i] ^ state[(state[x] + state[y]) & 255]);
		}

		return output;
	}


	private static bool TryNormalizeDecodedBytes(byte[] bytes, out string json)
	{
		json = string.Empty;

		var latin1 = Encoding.Latin1.GetString(bytes);
		if (!string.IsNullOrWhiteSpace(latin1) && TryNormalizeJsonText(latin1, out json))
			return true;

		var utf8 = Encoding.UTF8.GetString(bytes);
		if (!string.IsNullOrWhiteSpace(utf8) && TryNormalizeJsonText(utf8, out json))
			return true;

		return false;
	}

	private static bool TryNormalizeJsonText(string text, out string json)
	{
		json = string.Empty;
		var current = text.Trim();

		for (var i = 0; i < 5; i++)
		{
			if (!current.Contains('%'))
				break;

			var decoded = WebUtility.UrlDecode(current);
			if (string.Equals(decoded, current, StringComparison.Ordinal))
				break;

			current = decoded;
		}

		if (current.Length > 1 && current[0] == '"' && current[^1] == '"')
		{
			try
			{
				var unwrapped = JsonSerializer.Deserialize<string>(current);
				if (!string.IsNullOrWhiteSpace(unwrapped))
					current = unwrapped;
			}
			catch
			{
			}
		}

		if (TryParseJsonCandidate(current, out json))
			return true;

		if (!current.StartsWith('{') && !current.StartsWith('[') && (current.StartsWith("\"status\"", StringComparison.Ordinal) || current.StartsWith("\"result\"", StringComparison.Ordinal)))
		{
			var wrapped = "{" + current + "}";
			if (TryParseJsonCandidate(wrapped, out json))
				return true;
		}

		if (!current.StartsWith('{') && !current.StartsWith('[') && current.Contains("\":") && !current.Contains('%'))
		{
			var wrapped = "{" + current + "}";
			if (TryParseJsonCandidate(wrapped, out json))
				return true;
		}

		if (!current.StartsWith('{') && !current.StartsWith('[') && current.Contains('%'))
		{
			var htmlDecoded = WebUtility.HtmlDecode(current);
			if (!string.Equals(htmlDecoded, current, StringComparison.Ordinal) && TryNormalizeJsonText(htmlDecoded, out json))
				return true;
		}

		if (TryNormalizeAnchoredSegment(current, out json))
			return true;

		if (TryExtractJsonSegment(current, out json))
			return true;

		return false;
	}

	private static bool TryParseJsonCandidate(string candidate, out string json)
	{
		json = string.Empty;
		try
		{
			using var _ = JsonDocument.Parse(candidate);
			json = candidate;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryDecodeNestedBase64Layer(byte[] bytes, out byte[] decoded)
	{
		decoded = [];
		if (bytes.Length < 8)
			return false;

		var text = Encoding.Latin1.GetString(bytes).Trim();
		if (string.IsNullOrEmpty(text))
			return false;

		text = text.Trim('"');
		if (!LooksLikeBase64Text(text))
			return false;

		try
		{
			decoded = DecodeBase64Url(text);
			return decoded.Length > 0;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryNormalizeAnchoredSegment(string text, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(text))
			return false;

		var normalized = text.Trim();
		var statusAnchor = normalized.IndexOf("%22status%22", StringComparison.Ordinal);
		if (statusAnchor >= 0)
		{
			normalized = normalized[statusAnchor..];
		}
		else
		{
			statusAnchor = normalized.IndexOf("\"status\"", StringComparison.Ordinal);
			if (statusAnchor >= 0)
				normalized = normalized[statusAnchor..];
		}

		if (!normalized.Contains("status", StringComparison.Ordinal))
			return false;

		if (normalized.Contains('%'))
			normalized = WebUtility.UrlDecode(normalized);

		normalized = normalized.Trim();
		if (normalized.Length == 0)
			return false;

		if (!normalized.StartsWith('{') && !normalized.StartsWith('['))
			normalized = "{" + normalized + "}";

		if (TryParseJsonCandidate(normalized, out json))
			return true;

		var resultAnchor = normalized.IndexOf("\"result\"", StringComparison.Ordinal);
		if (resultAnchor > 0)
		{
			var candidate = "{" + normalized[resultAnchor..];
			if (TryParseJsonCandidate(candidate, out json))
				return true;
		}

		return false;
	}

	private static bool LooksLikeBase64Text(string value)
	{
		if (value.Length < 8)
			return false;

		foreach (var c in value)
		{
			if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c is '+' or '/' or '-' or '_' or '=')
				continue;
			return false;
		}

		return true;
	}

	private static bool TryExtractJsonSegment(string text, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(text))
			return false;

		var decoded = text;
		if (decoded.Contains('%'))
			decoded = WebUtility.UrlDecode(decoded);

		var start = decoded.IndexOf('{');
		if (start < 0)
			start = decoded.IndexOf('[');
		if (start < 0)
			return false;

		var candidate = decoded[start..].Trim();
		while (candidate.Length > 0)
		{
			if (TryParseJsonCandidate(candidate, out json))
				return true;

			var trimIndex = candidate.LastIndexOfAny(['}', ']']);
			if (trimIndex <= 0)
				break;

			candidate = candidate[..trimIndex].TrimEnd();
		}

		return false;
	}

	private static byte Add(byte value, int amount) => (byte)((value + amount) & 255);
	private static byte Subtract(byte value, int amount) => (byte)((value - amount + 256) & 255);
	private static byte Xor(byte value, int amount) => (byte)(value ^ amount);
	private static byte RotateRight1(byte value) => (byte)(((value << 1) | (value >> 7)) & 255);
	private static byte RotateRight2(byte value) => (byte)(((value >> 2) | (value << 6)) & 255);
	private static byte RotateLeft4(byte value) => (byte)(((value << 4) | (value >> 4)) & 255);
}
