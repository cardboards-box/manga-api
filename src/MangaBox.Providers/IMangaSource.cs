namespace MangaBox.Providers;

using Models;

public interface IMangaSource
{
    int Priority { get; }

    string ProviderName { get; }

    bool IsMatch(string url, Provider provider);

    Task<Boxed> Load(string url, Provider provider);

    Task<Boxed> Update(Series series, Provider provider);

    Task<FileMemoryResponse?> GetImage(Image image, Provider provider);
}
