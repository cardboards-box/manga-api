namespace MangaBox.Utilities.Comix;

/// <summary>
/// Seeded pseudo-random generators extracted from the JavaScript.
/// </summary>
internal sealed class ScrambleRandom(uint seed)
{
    private uint _state = seed;

    /// <summary>
    /// Equivalent to JavaScript:
    /// state = Math.imul(state, 1664525) + 1013904223
    /// </summary>
    public uint NextUInt32()
    {
        _state = unchecked((_state * 1664525u) + 1013904223u);
        return _state;
    }

    /// <summary>
    /// Returns a deterministic value in the range [0, maxExclusive).
    /// </summary>
    public int NextInt32(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Value must be greater than zero.");

        return (int)(NextUInt32() % (uint)maxExclusive);
    }

    public static int[] CreatePermutation(uint seed, int count)
    {
        return CreatePermutation(seed, count, ImageUnscrambler.ScrambleAlgorithm.LegacyLcg);
    }

    /// <summary>
    /// Recreates the seeded Fisher-Yates permutation from the JavaScript.
    /// </summary>
    public static int[] CreatePermutation(
        uint seed,
        int count,
        ImageUnscrambler.ScrambleAlgorithm algorithm)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Value cannot be negative.");

        var rng = Create(seed, algorithm);
        var values = Enumerable.Range(0, count).ToArray();

        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = rng.NextInt32(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        var inverse = new int[count];
        for (var i = 0; i < values.Length; i++)
            inverse[values[i]] = i;

        return inverse;
    }

    private static IScrambleRandom Create(uint seed, ImageUnscrambler.ScrambleAlgorithm algorithm)
    {
        return algorithm switch
        {
            ImageUnscrambler.ScrambleAlgorithm.LegacyLcg => new LcgScrambleRandom(seed),
            ImageUnscrambler.ScrambleAlgorithm.BuildOrderV2 => new BuildOrderV2ScrambleRandom(seed),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    private interface IScrambleRandom
    {
        int NextInt32(int maxExclusive);
    }

    private sealed class LcgScrambleRandom(uint seed) : IScrambleRandom
    {
        private readonly ScrambleRandom _rng = new(seed);

        public int NextInt32(int maxExclusive) => _rng.NextInt32(maxExclusive);
    }

    private sealed class BuildOrderV2ScrambleRandom(uint seed) : IScrambleRandom
    {
        private uint _state = seed | 1u;

        public int NextInt32(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Value must be greater than zero.");

            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;

            return (int)(_state % (uint)maxExclusive);
        }
    }
}
