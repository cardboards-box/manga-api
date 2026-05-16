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

	// Constant prefix/suffix bytes shared by all live tokens
	private static readonly byte[] LiveTokenPrefix =
	[
		97, 200, 64, 144, 162, 7, 176, 70, 112, 166, 46, 172, 221, 0, 253, 31, 196, 10, 25, 32,
		99, 136, 29, 229, 210, 13, 150, 51, 132, 252, 213, 72, 16, 222, 85, 16, 49, 197, 175, 230,
		100, 6, 120, 233, 32, 249, 167, 132, 106,
	];
	private static readonly byte[] LiveTokenSuffix  = [127, 241, 120, 206, 4, 167, 103, 234, 234, 27, 134];
	private static readonly byte[] LiveTokenSuffix4 = [239, 59, 144, 129, 223, 218, 212, 83, 12, 179, 59];

	// Positional lookup tables derived from live browser corpus.
	// LiveTable5[p * 36 + charIdx] = variable byte at position p for 5-char IDs.
	// LiveTable4[p * 36 + charIdx] = variable byte at position p for ≤4-char IDs.
	// charIdx: '0'=0..'9'=9, 'a'=10..'z'=35.  0 for unobserved chars.
	// chars: "0123456789abcdefghijklmnopqrstuvwxyz"
	private static readonly byte[] LiveTable5 =
	[
		// pos 0
		158, 157, 152, 135,   0, 153, 164, 163, 166, 165,   0,   0,   0, 138, 137,   0, 211,   0,   0, 208, 223, 210, 209, 220,   0,   0, 221, 216,   0,   0,   0, 228, 227, 230, 229, 224,
		// pos 1
		106, 107, 100,   0,   0, 111, 104, 105, 146, 147,   0,   0,   0,  62,  63,   0,  57,   0,   0,   0,  61,   6,   7,  32,   0,   0,  43,  36,   0,   0,   0,  40,  41,  82,   0,  44,
		// pos 2
		167, 199, 105, 137,   0,  72, 233,   9, 168, 200,   0,   0,   0,  46,  78,   0,  15,   0,   0, 116, 148,  47,  79, 244,   0,   0, 207, 113,   0,   0,   0,   0,  17, 176, 208, 110,
		// pos 3
		 50,  82, 114, 146,   0, 210,  43, 203, 107,  11,   0,   0,   0, 217, 120,   0, 185,   0,   0, 153,  57, 218, 121,  26,   0,   0, 250, 154,   0,   0,   0,   0, 179,  83, 243, 147,
		// pos 4
		 85, 117, 149, 181,   0,   0,  21,  53,  78, 110,   0,   0,   0, 199, 231,   0,  39,   0,   0, 136, 168, 200, 232,   8,   0,   0, 109, 141,   0,   0,   0,  13,   0,  70, 102, 134,
	];

	private static readonly byte[] LiveTable4 =
	[
		// pos 0
		158, 157,   0, 135,   0, 153,   0, 163, 166, 165,   0,   0,   0,   0, 137,   0,   0,   0,   0, 208, 223, 210, 209, 220,   0,   0, 221, 216,   0,   0,   0,   0,   0,   0,   0,   0,
		// pos 1
		  0, 107, 100, 101,   0,   0, 104, 105, 146, 147,   0,   0,   0,   0,  63,   0,   0,   0,   0,   0,  61,   0,   7,  32,   0,   0,   0,  36,   0,   0,   0,  40,  41,   0,  83,   0,
		// pos 2
		  0,   0, 105, 137,   0,   0, 233,   9, 168,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 116, 148,   0,  79,   0,   0,   0,   0, 113,   0,   0,   0,   0,  17,   0, 208,   0,
		// pos 3
		 50,  82,   0, 146,   0, 210,   0,   0, 107,   0,   0,   0,   0, 217, 120,   0, 185,   0,   0, 153,   0, 218, 121,   0,   0,   0,   0, 154,   0,   0,   0,  19,   0,   0,   0, 147,
	];

	// Chapter-detail endpoint uses a 59-byte token with a different prefix (52 constant bytes)
	// and 7 variable bytes driven by the numeric 7-digit chapter ID.
	// digits: "0123456789" (index = digit value)
	private static readonly byte[] LiveChapterPrefix =
	[
		97, 200, 64, 144, 162, 7, 176, 70, 112, 166, 46, 172, 221, 0, 253, 31, 196, 10, 25, 32,
		99, 136, 29, 229, 210, 13, 150, 51, 132, 252, 213, 72, 16, 222, 85, 208, 49, 100, 175, 133,
		100, 39, 120, 162, 32, 241, 167, 252, 185, 73, 100, 243,
	];

	private static readonly byte[] LiveTableChapter =
	[
		// pos 0: digits 0..9
		  0,   0,   0,   0,   0,   0,   0,   0, 107,  11,
		// pos 1
		 85, 117, 149, 181,   0,   0,   0,   0,  78, 110,
		// pos 2
		  0, 120,  59,   0, 187, 123,  58, 251, 190, 126,
		// pos 3
		  1,   0,   0, 249,  33,   0,  17,  25, 193, 201,
		// pos 4
		 15,  55,  31,   7, 232,  23, 248, 224, 200, 240,
		// pos 5
		228, 132,  36,   0, 100,   4, 164,  68, 229, 133,
		// pos 6
		 46, 238, 175, 110,   0, 233, 174, 105,  40, 232,
	];

	public static string SignChapter(string mangaId, int page, int limit = 100)
	{
		var path = $"manga/{mangaId}/chapters";
		var query = $"page={page}&limit={limit}&order%5Bnumber%5D=desc";

		// Current Comix signer is manga-id driven (live token), not legacy RC4 path signing.
		var sig = ComputeLiveSignature(mangaId);
		return $"{path}?{query}&_={sig}";
	}

	public static string SignChapter(string chapterId)
	{
		var path = $"chapters/{chapterId}";
		var sig = ComputeChapterSignature(chapterId);
		return $"{path}?_={sig}";
	}

	private static string ComputeChapterSignature(string chapterId)
	{
		const int varStart = 52, varCount = 7;

		var bytes = new byte[varStart + varCount];
		Buffer.BlockCopy(LiveChapterPrefix, 0, bytes, 0, LiveChapterPrefix.Length);

		for (var p = 0; p < varCount && p < chapterId.Length; p++)
		{
			var ci = chapterId[p] - '0';
			if (ci is >= 0 and <= 9)
				bytes[varStart + p] = LiveTableChapter[p * 10 + ci];
		}

		return Convert.ToBase64String(bytes)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	private static string ComputeLiveSignature(string id)
	{
		var isShort = id.Length <= 4;
		var table = isShort ? LiveTable4 : LiveTable5;
		var varCount = isShort ? 4 : 5;
		var suffix = isShort ? LiveTokenSuffix4 : LiveTokenSuffix;
		const int varStart = 49;

		var bytes = new byte[varStart + varCount + suffix.Length];
		Buffer.BlockCopy(LiveTokenPrefix, 0, bytes, 0, LiveTokenPrefix.Length);

		for (var p = 0; p < varCount && p < id.Length; p++)
		{
			var ci = CharToIdx(id[p]);
			if (ci >= 0)
				bytes[varStart + p] = table[p * 36 + ci];
		}

		Buffer.BlockCopy(suffix, 0, bytes, varStart + varCount, suffix.Length);

		return Convert.ToBase64String(bytes)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	private static int CharToIdx(char c) => c switch
	{
		>= '0' and <= '9' => c - '0',
		>= 'a' and <= 'z' => c - 'a' + 10,
		_ => -1,
	};

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