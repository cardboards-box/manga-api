namespace MangaBox;

public static class DiExtensions
{
    public static IDependencyResolver AddCore(this IDependencyResolver resolver)
    {
        return resolver
            .Transient<IRedisQueue, RedisQueue>()
            .AddServices(c => c
                .AddMangaDex()
                .AddRedis());
    }
}
