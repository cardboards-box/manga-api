namespace MangaBox.Caching;

using Models;
using Providers;

public interface IFileCacheService
{
    Task<FileResponse?> Get(Image image, bool force);
}

internal class FileCacheService(
    IDbService _db,
    IConfiguration _config,
    IImportService _import,
    IImagingService _imaging,
    ILogger<FileCacheService> _logger) : IFileCacheService
{
    public string CacheDirectory => _config["CacheDirectory"] 
        ?? throw new ArgumentNullException(nameof(CacheDirectory));

    public string GeneratePath(string hash, string group)
    {
        var dir = !string.IsNullOrEmpty(group) ? Path.Combine(CacheDirectory, group) : CacheDirectory;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return Path.Combine(dir, hash + ".data");
    }

    public static string DetermineFileName(string? current, string url)
    {
        if (!string.IsNullOrEmpty(current)) return current;

        return url.Split('/', StringSplitOptions.RemoveEmptyEntries).Last().Split('?').First();
    }

    public static string DetermineGroup(ImageType type)
    {
        return type switch 
        {
            ImageType.Cover => "manga-cover",
            ImageType.Page => "manga-page",
            _ => "external"
        };
    }

    public async Task<FileResponse?> Get(Image image, bool force)
    {
        try
        {
            if (!Directory.Exists(CacheDirectory))
                Directory.CreateDirectory(CacheDirectory);

            var group = DetermineGroup(image.Type);
            var path = GeneratePath(image.UrlHash, group);
            var expired = image.Expires <= DateTime.UtcNow;
            var refresh = expired ||
                !File.Exists(path) ||
                force ||
                image.Bytes is null ||
                string.IsNullOrEmpty(image.Name) ||
                string.IsNullOrEmpty(image.MimeType);

            if (!refresh)
                return new FileResponse(
                    File.OpenRead(path), 
                    image.Bytes!.Value, 
                    image.Name!, 
                    image.MimeType!);

            var process = await _import.Image(image);
            if (process is null) return null;

            process.Stream.Position = 0;
            var actions = new Func<Image, FileMemoryResponse, Task>[] 
            { 
                DoProps, 
                DoSizing, 
                DoHash,
                (i, r) => DoSave(i, r, path)
            };

            foreach(var action in actions)
                await action(image, process);
            
            await _db.Images.Upsert(image);

            process.Stream.Position = 0;

            return new FileResponse(
                process.Stream, 
                image.Bytes!.Value, 
                image.Name!, 
                image.MimeType!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get image {id}", image.Id);
            return null;
        }
    }

    public Task DoProps(Image image, FileMemoryResponse process)
    {
        image.Name = DetermineFileName(process.FileName ?? image.Name, image.Url);
        image.Bytes = process.Length;
        image.MimeType = process.MimeType;
        return Task.CompletedTask;
    }

    public async Task DoSizing(Image image, FileMemoryResponse process)
    {
        var (width, height) = await _imaging.GetSize(process.Stream);
        image.Width = width;
        image.Height = height;
        process.Stream.Position = 0;
    }

    public async Task DoHash(Image image, FileMemoryResponse process)
    {
        image.Hash = await _imaging.FileHash(process.Stream);
        process.Stream.Position = 0;
    }

    public static async Task DoSave(Image image, FileMemoryResponse process, string path)
    {
        using var oo = File.Create(path);
        await process.Stream.CopyToAsync(oo);
        image.CachedAt = DateTime.UtcNow;
        process.Stream.Position = 0;
    }
}
