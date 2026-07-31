using OpenCvSharp;

namespace MangaBox.Utilities.Comix;

/// <summary>
/// Provides utilities for determining the rotational difference between
/// a complete image and a rotated image containing its center portion.
/// </summary>
public static class CenterImageRotationMatcher
{
    /// <summary>
    /// Determines the angle by which a center image must be rotated to align
    /// with the corresponding center region of a larger target image.
    /// </summary>
    /// <param name="fullTarget">
    /// The complete target image containing the original, unrotated center region.
    /// </param>
    /// <param name="rotatedCenter">
    /// An image containing the center portion of <paramref name="fullTarget"/>,
    /// potentially rotated around its center.
    /// </param>
    /// <param name="targetCenter">
    /// The center point in <paramref name="fullTarget"/> corresponding to the center
    /// of <paramref name="rotatedCenter"/>.
    /// When <see langword="null"/>, the geometric center of
    /// <paramref name="fullTarget"/> is used.
    /// </param>
    /// <param name="usableRadiusFraction">
    /// The fraction of the maximum inscribed circular radius to compare.
    /// This must be greater than zero and less than or equal to one.
    /// A value around <c>0.9</c> usually avoids artifacts near the corners of
    /// a rotated square image.
    /// </param>
    /// <param name="angularResolution">
    /// The number of samples used to represent a complete 360-degree rotation.
    /// Higher values provide finer angular precision at the cost of additional
    /// processing time and memory.
    /// </param>
    /// <returns>
    /// The signed rotation angle, in degrees, required to align
    /// <paramref name="rotatedCenter"/> with <paramref name="fullTarget"/>.
    /// The returned value is in the range <c>(-180, 180]</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fullTarget"/> or
    /// <paramref name="rotatedCenter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when either image is empty, when the requested target crop extends
    /// beyond the bounds of <paramref name="fullTarget"/>, or when an image uses
    /// an unsupported channel count.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="usableRadiusFraction"/> is outside the range
    /// <c>(0, 1]</c>, or when <paramref name="angularResolution"/> is less than 360.
    /// </exception>
    public static double FindRotation(
        Mat fullTarget,
        Mat rotatedCenter,
        Point2f? targetCenter = null,
        double usableRadiusFraction = 0.9,
        int angularResolution = 3600)
    {
        ArgumentNullException.ThrowIfNull(fullTarget);
        ArgumentNullException.ThrowIfNull(rotatedCenter);

        if (fullTarget.Empty())
        {
            throw new ArgumentException(
                "The target image cannot be empty.",
                nameof(fullTarget));
        }

        if (rotatedCenter.Empty())
        {
            throw new ArgumentException(
                "The rotated center image cannot be empty.",
                nameof(rotatedCenter));
        }

        if (usableRadiusFraction is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usableRadiusFraction),
                usableRadiusFraction,
                "The usable radius fraction must be greater than zero and less than or equal to one.");
        }

        if (angularResolution < 360)
        {
            throw new ArgumentOutOfRangeException(
                nameof(angularResolution),
                angularResolution,
                "The angular resolution must be at least 360.");
        }

        Point2f resolvedTargetCenter = targetCenter ?? new Point2f(
            fullTarget.Width / 2f,
            fullTarget.Height / 2f);

        Rect targetCropBounds = CreateCenteredCropBounds(
            fullTarget,
            rotatedCenter.Size(),
            resolvedTargetCenter);

        using Mat targetCenterCrop = new(fullTarget, targetCropBounds);

        Point2f targetCropCenter = new(
            (targetCenterCrop.Width - 1) / 2f,
            (targetCenterCrop.Height - 1) / 2f);

        Point2f rotatedImageCenter = new(
            (rotatedCenter.Width - 1) / 2f,
            (rotatedCenter.Height - 1) / 2f);

        double maximumRadius =
            Math.Min(rotatedCenter.Width, rotatedCenter.Height) / 2.0;

        double usableRadius = maximumRadius * usableRadiusFraction;

        return FindRotationInternal(
            targetCenterCrop,
            rotatedCenter,
            targetCropCenter,
            rotatedImageCenter,
            usableRadius,
            angularResolution);
    }

    /// <summary>
    /// Load a Mat image from a Base64-encoded data URI string.
    /// </summary>
    /// <param name="dataUri">The Base64-encoded data URI string.</param>
    /// <returns>The loaded Mat image.</returns>
    /// <exception cref="FormatException">Thrown when the image string is not a valid data URI.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the image data cannot be decoded.</exception>
    public static Mat LoadDataUri(string dataUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataUri);

        int commaIndex = dataUri.IndexOf(',');

        if (commaIndex < 0)
        {
            throw new FormatException(
                "The image string is not a valid data URI.");
        }

        string metadata = dataUri[..commaIndex];
        string encodedData = dataUri[(commaIndex + 1)..];

        if (!metadata.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException(
                "The data URI does not contain an image.");
        }

        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException(
                "Only Base64-encoded image data URIs are supported.");
        }

        byte[] imageBytes;

        try
        {
            imageBytes = Convert.FromBase64String(encodedData);
        }
        catch (FormatException exception)
        {
            throw new FormatException(
                "The image data contains invalid Base64 content.",
                exception);
        }

        Mat image = Cv2.ImDecode(
            imageBytes,
            ImreadModes.Unchanged);

        if (image.Empty())
        {
            image.Dispose();

            throw new InvalidOperationException(
                "OpenCV could not decode the image data.");
        }

        return image;
    }

    /// <summary>
    /// Resizes the given material
    /// </summary>
    /// <param name="source">The source material</param>
    /// <param name="size">The size to resize to</param>
    /// <returns>The resized material</returns>
    /// <exception cref="ArgumentException">Thrown if the source material is null or empty</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the size is not positive</exception>
    public static Mat Resize(Mat source, int size)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Empty())
            throw new ArgumentException("The source image is empty.", nameof(source));

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "The size must be greater than zero.");
        }


        var resized = new Mat();
        var interpolation = source.Size().Width < size
            ? InterpolationFlags.Area
            : InterpolationFlags.Cubic;

        Cv2.Resize(source, resized, new Size(size, size), 0, 0, interpolation);
        return resized;
    }

    private static Rect CreateCenteredCropBounds(
        Mat target,
        Size cropSize,
        Point2f center)
    {
        int cropX = (int)Math.Round(
            center.X - ((cropSize.Width - 1) / 2.0));

        int cropY = (int)Math.Round(
            center.Y - ((cropSize.Height - 1) / 2.0));

        var bounds = new Rect(
            cropX,
            cropY,
            cropSize.Width,
            cropSize.Height);

        if (bounds.X < 0 ||
            bounds.Y < 0 ||
            bounds.Right > target.Width ||
            bounds.Bottom > target.Height)
        {
            throw new ArgumentException(
                "The center crop extends beyond the bounds of the target image. " +
                "Verify the target center and the size of the rotated center image.",
                nameof(center));
        }

        return bounds;
    }

    private static double FindRotationInternal(
        Mat target,
        Mat rotated,
        Point2f targetCenter,
        Point2f rotatedCenter,
        double radius,
        int angularResolution)
    {
        int radialResolution = Math.Max(
            32,
            (int)Math.Ceiling(radius));

        using Mat targetPolar = CreatePolarImage(
            target,
            targetCenter,
            radius,
            angularResolution,
            radialResolution);

        using Mat rotatedPolar = CreatePolarImage(
            rotated,
            rotatedCenter,
            radius,
            angularResolution,
            radialResolution);

        using Mat preparedTarget = PrepareForCorrelation(targetPolar);
        using Mat preparedRotated = PrepareForCorrelation(rotatedPolar);

        using Mat window = new();

        Cv2.CreateHanningWindow(
            window,
            preparedTarget.Size(),
            MatType.CV_32FC1);

        Point2d shift = Cv2.PhaseCorrelate(
            preparedRotated,
            preparedTarget,
            window,
            out _);

        double degreesPerPixel = 360.0 / angularResolution;
        double rotation = shift.X * degreesPerPixel;

        return NormalizeSignedDegrees(rotation);
    }

    private static Mat CreatePolarImage(
        Mat source,
        Point2f center,
        double radius,
        int angularResolution,
        int radialResolution)
    {
        using Mat polar = new();

        Cv2.WarpPolar(
            source,
            polar,
            new Size(radialResolution, angularResolution),
            center,
            radius,
            InterpolationFlags.Linear,
            WarpPolarMode.Linear);

        var transposed = new Mat();

        Cv2.Transpose(
            polar,
            transposed);

        return transposed;
    }

    private static Mat PrepareForCorrelation(Mat source)
    {
        using Mat grayscale = ConvertToGrayscale(source);
        using Mat floatImage = new();

        grayscale.ConvertTo(
            floatImage,
            MatType.CV_32FC1,
            1.0 / 255.0);

        using Mat blurred = new();

        Cv2.GaussianBlur(
            floatImage,
            blurred,
            new Size(0, 0),
            3);

        var highPassImage = new Mat();

        Cv2.Subtract(
            floatImage,
            blurred,
            highPassImage);

        return highPassImage;
    }

    private static Mat ConvertToGrayscale(Mat source)
    {
        var grayscale = new Mat();

        switch (source.Channels())
        {
            case 1:
                source.CopyTo(grayscale);
                break;

            case 3:
                Cv2.CvtColor(
                    source,
                    grayscale,
                    ColorConversionCodes.BGR2GRAY);
                break;

            case 4:
                Cv2.CvtColor(
                    source,
                    grayscale,
                    ColorConversionCodes.BGRA2GRAY);
                break;

            default:
                grayscale.Dispose();

                throw new ArgumentException(
                    $"Images with {source.Channels()} channels are not supported. " +
                    "Only grayscale, BGR, and BGRA images are supported.",
                    nameof(source));
        }

        return grayscale;
    }

    private static double NormalizeSignedDegrees(double degrees)
    {
        degrees %= 360.0;

        if (degrees > 180.0)
        {
            degrees -= 360.0;
        }
        else if (degrees <= -180.0)
        {
            degrees += 360.0;
        }

        return degrees;
    }
}