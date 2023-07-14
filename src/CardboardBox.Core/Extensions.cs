namespace CardboardBox;

public static class Extensions
{
    public static IEnumerable<T[]> Split<T>(this IEnumerable<T> data, int count)
    {
        var total = (int)Math.Ceiling((decimal)data.Count() / count);
        var current = new List<T>();

        foreach (var item in data)
        {
            current.Add(item);

            if (current.Count == total)
            {
                yield return current.ToArray();
                current.Clear();
            }
        }

        if (current.Count > 0) yield return current.ToArray();
    }

    public static Task AddCardboardServices(this IServiceCollection services, 
        IConfiguration config, 
        Action<IDependencyBuilder> configure)
    {
        var bob = new DependencyBuilder();
        configure(bob);
        return bob.Build(services, config);
    }
}
