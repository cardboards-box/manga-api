namespace CardboardBox.Manga.ReverseImageSearch;

using ReverseImageSearch.GoogleVision;
using ReverseImageSearch.MatchApi;
using ReverseImageSearch.NsfwCheck;
using ReverseImageSearch.SauceNao;

public static class Extensions
{
    public static IDependencyBuilder AddReverseImage(this IDependencyBuilder builder)
    {
        return builder
            .Transient<IReverseSearchService, ReverseSearchService>()
            .Transient<IMatchApiService, MatchApiService>()
            .Transient<IMatchService, MatchService>()
            .Transient<ISauceNaoApiService, SauceNaoApiService>()
            .Transient<INsfwApiService, NsfwApiService>()
            .Transient<IGoogleVisionService, GoogleVisionService>()
            .Transient<IMatchIndexingService, MatchIndexingService>();
    }
}
