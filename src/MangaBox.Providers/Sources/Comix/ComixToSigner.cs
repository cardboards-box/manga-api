using System.Numerics;

namespace MangaBox.Providers.Sources.Comix;

public static class ComixToSigner
{
	// RC4 keys (base64-encoded 32-byte AES-like keys for each signing round)
	private static readonly byte[][] Rc4Keys = [
		Convert.FromBase64String("13YDu67uDgFczo3DnuTIURqas4lfMEPADY6Jaeqky+w="),
		Convert.FromBase64String("vZ23RT7pbSlxwiygkHd1dhToIku8SNHPC6V36L4cnwM="),
		Convert.FromBase64String("BkWI8feqSlDZKMq6awfzWlUypl88nz65KVRmpH0RWIc="),
		Convert.FromBase64String("RougjiFHkSKs20DZ6BWXiWwQUGZXtseZIyQWKz5eG34="),
		Convert.FromBase64String("U9LRYFL2zXU4TtALIYDj+lCATRk/EJtH7/y7qYYNlh8="),
	];

	// XOR keys (base64-encoded 32-byte keys applied after RC4 in each round)
	private static readonly byte[][] XorKeys = [
		Convert.FromBase64String("yEy7wBfBc+gsYPiQL/4Dfd0pIBZFzMwrtlRQGwMXy3Q="),
		Convert.FromBase64String("QX0sLahOByWLcWGnv6l98vQudWqdRI3DOXBdit9bxCE="),
		Convert.FromBase64String("v7EIpiQQjd2BGuJzMbBA0qPWDSS+wTJRQ7uGzZ6rJKs="),
		Convert.FromBase64String("LL97cwoDoG5cw8QmhI+KSWzfW+8VehIh+inTxnVJ2ps="),
		Convert.FromBase64String("e/GtffFDTvnw7LBRixAD+iGixjqTq9kIZ1m0Hj+s6fY="),
	];

	// Prepend keys: interleaved before the transformed bytes for the first N bytes of each round
	private static readonly byte[][] PrependKeys = [
		Convert.FromBase64String("yrP+EVA1Dw=="),
		Convert.FromBase64String("WJwgqCmf"),
		Convert.FromBase64String("1SUReYlCRA=="),
		Convert.FromBase64String("52iDqjzlqe8="),
		Convert.FromBase64String("xb2XwHNB"),
	];

	// Number of prepend bytes interleaved in each round
	private static readonly int[] PrependCounts = [7, 6, 7, 8, 6];

	private static readonly byte[] LiveTokenPrefix =
	[
		97, 200, 64, 144, 162, 7, 176, 70, 112, 166, 46, 172, 221, 0, 253, 31, 196, 10, 25, 32,
		99, 136, 29, 229, 210, 13, 150, 51, 132, 252, 213, 72, 16, 222, 85, 16, 49, 197, 175, 230,
		100, 6, 120, 233, 32, 249, 167, 132, 106,
	];

	private static readonly ulong[] LiveVariableBitMasks =
	[
		105991738, 51780415, 106648650, 101453670, 102369579, 102369547, 1443341, 32,
		105317703, 122032177, 4933456, 5785966, 38870390, 71703670, 105729616, 119299905,
		4212491, 17781050, 122031462, 123668029, 100866919, 105336076, 102702646, 18498907,
		118693414, 73028896, 34692474, 50988358, 35277078, 55513408, 102254145, 5330176,
		122228004, 329020, 84033793, 34622328, 0, 21825298, 101321551, 123162950,
	];

	private static readonly byte[] LiveTokenSuffix = [127, 241, 120, 206, 4, 167, 103, 234, 234, 27, 134];

	public static string SignChapter(string mangaId, int page, int limit = 20)
	{
		var path = $"manga/{mangaId}/chapters";
		var query = $"page={page}&limit={limit}&order%5Bnumber%5D=desc";

		// Current Comix signer is manga-id driven (live token), not legacy RC4 path signing.
		var sig = ComputeLiveSignature(mangaId);
		return $"{path}?{query}&_={sig}";
	}

	private static string ComputeLiveSignature(string mangaId)
	{
		var bytes = new byte[65];
		Buffer.BlockCopy(LiveTokenPrefix, 0, bytes, 0, LiveTokenPrefix.Length);

		var featureBits = BuildFeatureBits(mangaId);
		for (var outputBit = 0; outputBit < LiveVariableBitMasks.Length; outputBit++)
		{
			if (Parity(featureBits & LiveVariableBitMasks[outputBit]) != 0)
			{
				var byteIndex = 49 + (outputBit >> 3);
				var bitIndex = outputBit & 7;
				bytes[byteIndex] |= (byte)(1 << bitIndex);
			}
		}

		Buffer.BlockCopy(LiveTokenSuffix, 0, bytes, 54, LiveTokenSuffix.Length);

		return Convert.ToBase64String(bytes)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	private static ulong BuildFeatureBits(string mangaId)
	{
		ulong bits = 0;
		var bitIndex = 0;

		for (var i = 0; i < 5; i++)
		{
			var ch = i < mangaId.Length ? (byte)mangaId[i] : (byte)0;
			for (var bit = 0; bit < 8; bit++)
			{
				if (((ch >> bit) & 1) != 0)
				{
					bits |= 1UL << bitIndex;
				}

				bitIndex++;
			}
		}

		return bits;
	}

	private static int Parity(ulong value)
	{
		return (int)(BitOperations.PopCount(value) & 1);
	}

	private static string ComputeSignature(string url)
	{
		var message = Uri.EscapeDataString($"{url}:0:1");
		var bytes = Encoding.Latin1.GetBytes(message);

		for (var round = 0; round < 5; round++)
		{
			var rc4Out = Rc4(Rc4Keys[round], bytes);
			var pc = PrependCounts[round];
			var output = new byte[bytes.Length + pc];
			var outIdx = 0;

			for (var i = 0; i < bytes.Length; i++)
			{
				if (i < pc) output[outIdx++] = PrependKeys[round][i];
				var xored = (byte)(rc4Out[i] ^ XorKeys[round][i % 32]);
				output[outIdx++] = Transform(round, i % 10, xored);
			}

			bytes = output;
		}

		return Convert.ToBase64String(bytes)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	private static byte Transform(int round, int pos, byte v) => (round, pos) switch
	{
		// Round 0: c, b, y, $, h, s, h, k, y, c
		(0, 0) or (0, 9) => (byte)((v + 115) & 0xFF),
		(0, 1)           => (byte)((v - 12 + 256) & 0xFF),
		(0, 2) or (0, 8) => (byte)(((v >> 1) | (v << 7)) & 0xFF),
		(0, 3)           => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(0, 4) or (0, 6) => (byte)((v - 42 + 256) & 0xFF),
		(0, 5)           => (byte)((v + 143) & 0xFF),
		(0, 7)           => (byte)((v + 15) & 0xFF),

// Round 1: c, b, $, h, s, k, $, _, c, s
		(1, 0) or (1, 8)           => (byte)((v + 115) & 0xFF),
		(1, 1)                     => (byte)((v - 12 + 256) & 0xFF),
		(1, 2) or (1, 6)           => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(1, 3)                     => (byte)((v - 42 + 256) & 0xFF),
		(1, 4) or (1, 9)           => (byte)((v + 143) & 0xFF),
		(1, 5)                     => (byte)((v + 15) & 0xFF),
		(1, 7)                     => (byte)((v - 20 + 256) & 0xFF),

		// Round 2: c, f, s, g, y, m, $, k, s, b
		(2, 0)           => (byte)((v + 115) & 0xFF),
		(2, 1)           => (byte)((v - 188 + 256) & 0xFF),
		(2, 2) or (2, 8) => (byte)((v + 143) & 0xFF),
		(2, 3)           => (byte)(((v << 2) | (v >> 6)) & 0xFF),
		(2, 4)           => (byte)(((v >> 1) | (v << 7)) & 0xFF),
		(2, 5)           => (byte)(v ^ 177),
		(2, 6)           => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(2, 7)           => (byte)((v + 15) & 0xFF),
		(2, 9)           => (byte)((v - 12 + 256) & 0xFF),

		// Round 3: b, m, y, s, _, s, _, y, y, m
		(3, 0)                     => (byte)((v - 12 + 256) & 0xFF),
		(3, 1) or (3, 9)           => (byte)(v ^ 177),
		(3, 2) or (3, 7) or (3, 8) => (byte)(((v >> 1) | (v << 7)) & 0xFF),
		(3, 3) or (3, 5)           => (byte)((v + 143) & 0xFF),
		(3, 4) or (3, 6)           => (byte)((v - 20 + 256) & 0xFF),

		// Round 4: _, s, c, m, b, m, f, s, $, g
		(4, 0)           => (byte)((v - 20 + 256) & 0xFF),
		(4, 1) or (4, 7) => (byte)((v + 143) & 0xFF),
		(4, 2)           => (byte)((v + 115) & 0xFF),
		(4, 3) or (4, 5) => (byte)(v ^ 177),
		(4, 4)           => (byte)((v - 12 + 256) & 0xFF),
		(4, 6)           => (byte)((v - 188 + 256) & 0xFF),
		(4, 8)           => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(4, 9)           => (byte)(((v << 2) | (v >> 6)) & 0xFF),

		_ => v,
	};

	private static byte[] Rc4(byte[] key, byte[] data)
	{
		var s = new byte[256];
		for (var i = 0; i < 256; i++) s[i] = (byte)i;

		var j = 0;
		for (var i = 0; i < 256; i++)
		{
			j = (j + s[i] + key[i % key.Length]) % 256;
			(s[i], s[j]) = (s[j], s[i]);
		}

		var result = new byte[data.Length];
		var x = 0; var y = 0;
		for (var i = 0; i < data.Length; i++)
		{
			x = (x + 1) % 256;
			y = (y + s[x]) % 256;
			(s[x], s[y]) = (s[y], s[x]);
			result[i] = (byte)(data[i] ^ s[(s[x] + s[y]) % 256]);
		}
		return result;
	}

	internal static byte[] DecodeBase64Url(string value)
	{
		var padded = value.Trim().Replace('-', '+').Replace('_', '/');
		switch (padded.Length % 4)
		{
			case 2: padded += "=="; break;
			case 3: padded += "="; break;
			case 1: throw new FormatException("Invalid base64 length.");
		}
		return Convert.FromBase64String(padded);
	}

	internal static byte[] ReverseRound(int round, byte[] output)
	{
		var pc = PrependCounts[round];
		if (output.Length < pc)
			return output;

		var inputLength = output.Length - pc;
		var rc4Out = new byte[inputLength];
		var outIdx = 0;

		for (var i = 0; i < inputLength; i++)
		{
			if (i < pc)
			{
				outIdx++; // skip prepend byte
			}

			if (outIdx >= output.Length)
				break;

			var transformed = output[outIdx++];
			var untransformed = ReverseTransform(round, i % 10, transformed);
			rc4Out[i] = (byte)(untransformed ^ XorKeys[round][i % 32]);
		}

		return Rc4(Rc4Keys[round], rc4Out);
	}

	private static byte ReverseTransform(int round, int pos, byte v) => (round, pos) switch
	{
		(0, 0) or (0, 9) => (byte)((v - 115 + 256) & 0xFF),
		(0, 1)           => (byte)((v + 12) & 0xFF),
		(0, 2) or (0, 8) => (byte)(((v << 1) | (v >> 7)) & 0xFF),
		(0, 3)           => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(0, 4) or (0, 6) => (byte)((v + 42) & 0xFF),
		(0, 5)           => (byte)((v - 143 + 256) & 0xFF),
		(0, 7)           => (byte)((v - 15 + 256) & 0xFF),

		(1, 0) or (1, 8) => (byte)((v - 115 + 256) & 0xFF),
		(1, 1)           => (byte)((v + 12) & 0xFF),
		(1, 2) or (1, 6) => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(1, 3)           => (byte)((v + 42) & 0xFF),
		(1, 4) or (1, 9) => (byte)((v - 143 + 256) & 0xFF),
		(1, 5)           => (byte)((v - 15 + 256) & 0xFF),
		(1, 7)           => (byte)((v + 20) & 0xFF),

		(2, 0)           => (byte)((v - 115 + 256) & 0xFF),
		(2, 1)           => (byte)((v + 188) & 0xFF),
		(2, 2) or (2, 8) => (byte)((v - 143 + 256) & 0xFF),
		(2, 3)           => (byte)(((v >> 2) | (v << 6)) & 0xFF),
		(2, 4)           => (byte)(((v << 1) | (v >> 7)) & 0xFF),
		(2, 5)           => (byte)(v ^ 177),
		(2, 6)           => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(2, 7)           => (byte)((v - 15 + 256) & 0xFF),
		(2, 9)           => (byte)((v + 12) & 0xFF),

		(3, 0)                     => (byte)((v + 12) & 0xFF),
		(3, 1) or (3, 9)           => (byte)(v ^ 177),
		(3, 2) or (3, 7) or (3, 8) => (byte)(((v << 1) | (v >> 7)) & 0xFF),
		(3, 3) or (3, 5)           => (byte)((v - 143 + 256) & 0xFF),
		(3, 4) or (3, 6)           => (byte)((v + 20) & 0xFF),

		(4, 0)           => (byte)((v + 20) & 0xFF),
		(4, 1) or (4, 7) => (byte)((v - 143 + 256) & 0xFF),
		(4, 2)           => (byte)((v - 115 + 256) & 0xFF),
		(4, 3) or (4, 5) => (byte)(v ^ 177),
		(4, 4)           => (byte)((v + 12) & 0xFF),
		(4, 6)           => (byte)((v + 188) & 0xFF),
		(4, 8)           => (byte)(((v << 4) | (v >> 4)) & 0xFF),
		(4, 9)           => (byte)(((v >> 2) | (v << 6)) & 0xFF),

		_ => v,
	};
}