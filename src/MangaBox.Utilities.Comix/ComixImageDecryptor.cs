namespace MangaBox.Utilities.Comix;

/// <summary>
/// Decrypts Comix image responses that have an encoded byte prefix.
/// </summary>
public static class ComixImageDecryptor
{
    /// <summary>
    /// The HTTP header that contains the length of the encrypted prefix in bytes.
    /// </summary>
    public const string ENC_LEN_HEADER = "X-Enc-Len";
    /// <summary>
    /// The HTTP header that contains the seed used for the encryption of the prefix.
    /// </summary>
    public const string ENC_SEED_HEADER = "X-Enc-Seed";
    /// <summary>
    /// The HTTP header that contains the algorithm used for the encryption of the prefix.
    /// </summary>
    public const string ENC_ALGO_HEADER = "X-Enc-Algo";

    private const uint ENC_MULTIPLIER = 1000005u;
    private const uint ENC_INCREMENT = 1234567891u;

    /// <summary>
    /// Decrypts the prefix of a file in place, using the specified seed and length from the headers.
    /// </summary>
    /// <param name="path">The path to the file to decrypt.</param>
    /// <param name="seedHeader">The header containing the seed for encryption.</param>
    /// <param name="lengthHeader">The header containing the length of the encrypted prefix.</param>
    /// <param name="algorithmHeader">The header containing the encryption algorithm.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task DecryptFilePrefixAsync(
        string path,
        string seedHeader,
        string lengthHeader,
        string? algorithmHeader,
        CancellationToken token = default)
    {
        var seed = ParseSeed(seedHeader);
        var length = ParseLength(lengthHeader);
        var algorithm = ParseAlgorithm(algorithmHeader);

        if (seed == 0 || length <= 0)
            return;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 8192,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var limit = (int)Math.Min(length, stream.Length);
        if (limit <= 0)
            return;

        var bytes = new byte[limit];
        var read = 0;
        while (read < limit)
        {
            var count = await stream.ReadAsync(bytes.AsMemory(read, limit - read), token);
            if (count <= 0)
                break;

            read += count;
        }

        DecryptPrefix(bytes.AsSpan(0, read), seed, read, algorithm);

        stream.Position = 0;
        await stream.WriteAsync(bytes.AsMemory(0, read), token);
        await stream.FlushAsync(token);
    }

    internal enum EncryptionAlgorithm
    {
        XorPrefixV1 = 1,
        XorPrefixV2 = 2
    }

    internal static void DecryptPrefix(Span<byte> bytes, uint seed, int length, EncryptionAlgorithm algorithm)
    {
        var limit = Math.Min(bytes.Length, length);

        switch (algorithm)
        {
            case EncryptionAlgorithm.XorPrefixV1:
                DecryptPrefixV1(bytes[..limit], seed);
                break;
            case EncryptionAlgorithm.XorPrefixV2:
                DecryptPrefixV2(bytes[..limit], seed);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null);
        }
    }

    private static void DecryptPrefixV1(Span<byte> bytes, uint seed)
    {
        var state = seed;

        for (var i = 0; i < bytes.Length; i++)
        {
            state = unchecked((state * ENC_MULTIPLIER) + ENC_INCREMENT);
            bytes[i] = (byte)(bytes[i] ^ (state >> 24));
        }
    }

    private static void DecryptPrefixV2(Span<byte> bytes, uint seed)
    {
        if (seed == 0)
            return;

        var candidates = new[]
        {
                DecodeWithXorshift(bytes, seed | 1u, highByte: false),
                DecodeWithXorshift(bytes, seed, highByte: false),
                DecodeWithXorshift(bytes, seed | 1u, highByte: true),
                DecodeWithLcg(bytes, seed)
            };

        var decoded = candidates.FirstOrDefault(HasImageSignature) ?? candidates[0];
        decoded.CopyTo(bytes);
    }

    private static byte[] DecodeWithXorshift(ReadOnlySpan<byte> bytes, uint initialState, bool highByte)
    {
        var result = bytes.ToArray();
        var state = initialState;

        for (var i = 0; i < result.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;

            var key = highByte ? state >> 24 : state & 0xff;
            result[i] = (byte)(result[i] ^ key);
        }

        return result;
    }

    private static byte[] DecodeWithLcg(ReadOnlySpan<byte> bytes, uint seed)
    {
        var result = bytes.ToArray();
        DecryptPrefixV1(result, seed);
        return result;
    }

    private static bool HasImageSignature(byte[] bytes)
    {
        if (bytes.Length < 12)
            return false;

        return bytes[0] == 'R' &&
            bytes[1] == 'I' &&
            bytes[2] == 'F' &&
            bytes[3] == 'F' &&
            bytes[8] == 'W' &&
            bytes[9] == 'E' &&
            bytes[10] == 'B' &&
            bytes[11] == 'P' ||
            bytes[0] == 0xff &&
            bytes[1] == 0xd8 ||
            bytes[0] == 0x89 &&
            bytes[1] == 'P' &&
            bytes[2] == 'N' &&
            bytes[3] == 'G';
    }

    internal static uint ParseSeed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Missing X-Enc-Seed header.", nameof(value));

        if (!uint.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
            throw new FormatException($"Invalid X-Enc-Seed value: {value}");

        return seed;
    }

    internal static int ParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Missing X-Enc-Len header.", nameof(value));

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var length))
            throw new FormatException($"Invalid X-Enc-Len value: {value}");

        if (length < 0)
            throw new FormatException($"Invalid X-Enc-Len value: {value}");

        return length;
    }

    internal static EncryptionAlgorithm ParseAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EncryptionAlgorithm.XorPrefixV1;

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var algorithm))
            throw new FormatException($"Invalid X-Enc-Algo value: {value}");

        return algorithm switch
        {
            1 => EncryptionAlgorithm.XorPrefixV1,
            2 => EncryptionAlgorithm.XorPrefixV2,
            _ => throw new FormatException($"Unsupported X-Enc-Algo value: {value}")
        };
    }
}
