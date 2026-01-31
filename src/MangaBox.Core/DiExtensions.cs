using MangaDexSharp;

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

    public static bool EqualsIc(this string? first, string second)
    {
        if (string.IsNullOrEmpty(first)) return false;

        return first.Equals(second, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool ContainsIc(this string? first, string second)
    {
        if (string.IsNullOrEmpty(first)) return false;

        return first.Contains(second, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool StartsWithIc(this string? first, string second)
    {
        if (string.IsNullOrEmpty(first)) return false;

        return first.StartsWith(second, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool EndsWithIc(this string? first, string second)
    {
        if (string.IsNullOrEmpty(first)) return false;

        return first.EndsWith(second, StringComparison.InvariantCultureIgnoreCase);
    }
}
