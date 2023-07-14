using Microsoft.Extensions.Configuration;

namespace CardboardBox.Manga.Tests;

public static class TestHelper
{
    private static async Task<IServiceProvider> GenerateProvider(Action<IDependencyBuilder> configure)
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var bob = new DependencyBuilder();
        configure(bob);

        await bob.RegisterServices(services, config);
        return services.BuildServiceProvider();
    }

    public static Task<IServiceProvider> ServiceProvider()
    {
        return GenerateProvider(c =>
        {
            c.AddMangaSources();
        });
    }
}
