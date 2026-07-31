using SkiaSharp;

namespace MangaBox.Utilities.Comix;

using Services.Imaging;

/// <summary>
/// Unscrambles images that were scrambled using a seeded grid permutation.
/// </summary>
public static class ImageUnscrambler
{
    /// <summary>
    /// Indicates how the permutation is interpreted when unscrambling an image.
    /// </summary>
    public enum PermutationMode
    {
        /// <summary>
        /// Scrambled tile position i contains the original tile permutation[i].
        /// </summary>
        ScrambledPositionContainsOriginalIndex,

        /// <summary>
        /// Original tile position i was moved to scrambled tile position permutation[i].
        /// </summary>
        OriginalIndexMovedToScrambledPosition
    }

    /// <summary>
    /// Unscrambles an image that was scrambled using a seeded grid permutation.
    /// </summary>
    /// <param name="image">The scrambled image to unscramble.</param>
    /// <param name="scrambleSeedHeader">The header containing the seed for the scramble algorithm.</param>
    /// <param name="scrambleGridHeader">The header containing the grid dimensions for the scramble algorithm.</param>
    /// <param name="scrambleAlgorithmHeader">The header containing the algorithm used for the scramble.</param>
    /// <param name="scrambleHashHeader">The header containing the hash for additional seed salting.</param>
    /// <param name="mode">Indicates how the permutation is interpreted when unscrambling an image.</param>
    /// <returns>The unscrambled image.</returns>
    public static SKBitmap Unscramble(
        SKBitmap image,
        string scrambleSeedHeader,
        string scrambleGridHeader,
        string? scrambleAlgorithmHeader,
        string? scrambleHashHeader,
        PermutationMode mode = PermutationMode.OriginalIndexMovedToScrambledPosition)
    {
        var seed = ParseSeed(scrambleSeedHeader);
        var (columns, rows) = ParseGrid(scrambleGridHeader);
        var algorithm = ParseAlgorithm(scrambleAlgorithmHeader);
        seed = ApplyHashSeedSalt(seed, algorithm, scrambleHashHeader);
        return Unscramble(image, seed, columns, rows, algorithm, mode);
    }

    internal enum ScrambleAlgorithm
    {
        LegacyLcg = 1,
        BuildOrderV2 = 3
    }

    private static uint ApplyHashSeedSalt(uint seed, ScrambleAlgorithm algorithm, string? hashHeader)
    {
        if (algorithm != ScrambleAlgorithm.BuildOrderV2 || string.IsNullOrWhiteSpace(hashHeader))
            return seed;

        return hashHeader.Trim() switch
        {
            "03632" => seed ^ 58414u,
            "02900" => seed ^ 117532u,
            _ => seed
        };
    }

    internal static SKBitmap Unscramble(
        SKBitmap scrambled,
        uint seed,
        int columns,
        int rows,
        ScrambleAlgorithm algorithm,
        PermutationMode mode = PermutationMode.OriginalIndexMovedToScrambledPosition)
    {
        ArgumentNullException.ThrowIfNull(scrambled);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);

        var tileWidth = scrambled.Width / columns;
        var tileHeight = scrambled.Height / rows;

        if (tileWidth <= 0 || tileHeight <= 0)
            throw new InvalidOperationException("Image is too small for the requested scramble grid.");

        var tileCount = columns * rows;
        var permutation = ScrambleRandom.CreatePermutation(seed, tileCount, algorithm);

        // Clone instead of creating a blank image so any right/bottom remainder pixels
        // that were not part of the fixed-size scramble grid are preserved.
        var output = SkiaImageHelpers.CreateBitmap(scrambled.Width, scrambled.Height);
        CopyPixels(
            source: scrambled,
            sourceRect: new SKRectI(0, 0, scrambled.Width, scrambled.Height),
            destination: output,
            destinationPoint: SKPointI.Empty);

        for (var scrambledIndex = 0; scrambledIndex < tileCount; scrambledIndex++)
        {
            var sourceIndex = mode switch
            {
                PermutationMode.ScrambledPositionContainsOriginalIndex => scrambledIndex,
                PermutationMode.OriginalIndexMovedToScrambledPosition => permutation[scrambledIndex],
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            var destinationIndex = mode switch
            {
                PermutationMode.ScrambledPositionContainsOriginalIndex => permutation[scrambledIndex],
                PermutationMode.OriginalIndexMovedToScrambledPosition => scrambledIndex,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            var sourceRect = GetFixedTileRectangle(sourceIndex, tileWidth, tileHeight, columns);
            var destinationPoint = GetFixedTilePoint(destinationIndex, tileWidth, tileHeight, columns);

            CopyPixels(scrambled, sourceRect, output, destinationPoint);
        }

        return output;
    }

    private static SKRectI GetFixedTileRectangle(
        int index,
        int tileWidth,
        int tileHeight,
        int columns)
    {
        var column = index % columns;
        var row = index / columns;

        return new SKRectI(
            column * tileWidth,
            row * tileHeight,
            (column + 1) * tileWidth,
            (row + 1) * tileHeight);
    }

    private static SKPointI GetFixedTilePoint(
        int index,
        int tileWidth,
        int tileHeight,
        int columns)
    {
        var column = index % columns;
        var row = index / columns;

        return new SKPointI(
            column * tileWidth,
            row * tileHeight);
    }

    private static void CopyPixels(
        SKBitmap source,
        SKRectI sourceRect,
        SKBitmap destination,
        SKPointI destinationPoint)
    {
        using var canvas = new SKCanvas(destination);
        var destinationRect = new SKRect(
            destinationPoint.X,
            destinationPoint.Y,
            destinationPoint.X + sourceRect.Width,
            destinationPoint.Y + sourceRect.Height);
        canvas.DrawBitmap(source, sourceRect, destinationRect, SKSamplingOptions.Default);
        canvas.Flush();
    }

    internal static uint ParseSeed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Missing X-Scramble-Seed header.", nameof(value));

        if (!uint.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
            throw new FormatException($"Invalid X-Scramble-Seed value: {value}");

        return seed;
    }

    internal static (int Columns, int Rows) ParseGrid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (5, 5);

        value = value.Trim();

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var single))
            return (single, single);

        var match = Regex.Match(value, @"^\s*(\d+)\s*[xX,\s]\s*(\d+)\s*$");
        if (!match.Success)
            throw new FormatException($"Invalid X-Scramble-Grid value: {value}");

        var columns = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var rows = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        if (columns <= 0 || rows <= 0)
            throw new FormatException($"Invalid X-Scramble-Grid value: {value}");

        return (columns, rows);
    }

    internal static ScrambleAlgorithm ParseAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ScrambleAlgorithm.LegacyLcg;

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var algorithm))
            throw new FormatException($"Invalid X-Scramble-Algo value: {value}");

        return algorithm switch
        {
            1 => ScrambleAlgorithm.LegacyLcg,
            2 => ScrambleAlgorithm.LegacyLcg,
            3 => ScrambleAlgorithm.BuildOrderV2,
            _ => throw new FormatException($"Unsupported X-Scramble-Algo value: {value}")
        };
    }
}
