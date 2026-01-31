namespace MangaBox;

using Caching;

public static class DiExtensions
{
    public static IDependencyResolver AddCaching(this IDependencyResolver resolver)
    {
        return resolver
            .Transient<IFileCacheService, FileCacheService>()
            .Transient<IImagingService, ImagingService>();
    }
}
