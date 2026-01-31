namespace MangaBox;

using Providers;
using Providers.ThirdParty.MangaDex;

public static class DiExtensions
{
    public static IDependencyResolver AddProviders(this IDependencyResolver resolver)
    {
        return resolver
            .Transient<IImportService, ImportService>()
            .Transient<ISourceProviderService, SourceProviderService>()

            .Transient<IMangaSource, MangaDexImporter>()
            .Transient<IMangaDexInteropService, MangaDexInteropService>();
    }
}
