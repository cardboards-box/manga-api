namespace CardboardBox;

using Core;

public static class Extensions
{
    public static IDependencyBuilder AddCore(this IDependencyBuilder services)
    {
        return services
            .AddServices(c =>
            {
                c.AddSerilog()
                 .AddCardboardHttp();
            });
    }

    public static Task AddCardboardServices(this IServiceCollection services, 
        IConfiguration config, 
        Action<IDependencyBuilder> configure)
    {
        var bob = new DependencyBuilder();
        bob.AddCore();
        configure(bob);
        return bob.Build(services, config);
    }
}
