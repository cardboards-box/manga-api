using System.Security.Cryptography;
using ISImage = SixLabors.ImageSharp.Image;

namespace MangaBox.Caching;

public interface IImagingService
{
    Task<string> FileHash(Stream io);

    Task<(int? width, int? height)> GetSize(Stream io);
}

internal class ImagingService(
    ILogger<ImagingService> _logger) : IImagingService
{
    public async Task<string> FileHash(Stream io)
    {
        using var hasher = SHA512.Create();
        var hash = await hasher.ComputeHashAsync(io);
        return hash.ToHexString();
    }

    public async Task<(int? width, int? height)> GetSize(Stream io)
    {
        try
        {
            using var image = await ISImage.LoadAsync(io);
            return (image.Width, image.Height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get image size.");
            return (null, null);
        }
    }
}
