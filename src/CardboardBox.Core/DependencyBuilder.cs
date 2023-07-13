using Dapper;
using System.Data;

namespace CardboardBox;

using Core;

public interface IDependencyBuilder
{
    IDependencyBuilder AddServices(Func<IServiceCollection, Task> services);

    IDependencyBuilder AddServices(Func<IServiceCollection, IConfiguration, Task> services);

    IDependencyBuilder AddServices(Action<IServiceCollection, IConfiguration> services);

    IDependencyBuilder AddServices(Action<IServiceCollection> services);

    IDependencyBuilder Model<T>();

    IDependencyBuilder JsonModel<T>(Func<T> @default);

    IDependencyBuilder JsonModel<T>();

    IDependencyBuilder Transient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    IDependencyBuilder Singleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    IDependencyBuilder Singleton<TService>(TService instance)
        where TService : class;
}

public class DependencyBuilder : IDependencyBuilder
{
    private readonly List<Func<IServiceCollection, IConfiguration, Task>> _services = new();
    private readonly List<Action<IConventionBuilder>> _conventions = new();
    private readonly List<Action<ITypeMapBuilder>> _dbMapping = new();

    public IDependencyBuilder AddServices(Func<IServiceCollection, IConfiguration, Task> services)
    {
        _services.Add(services);
        return this;
    }

    public IDependencyBuilder AddServices(Func<IServiceCollection, Task> services)
    {
        return AddServices((s, _) => services(s));
    }

    public IDependencyBuilder AddServices(Action<IServiceCollection, IConfiguration> services)
    {
        return AddServices((s, c) =>
        {
            services(s, c);
            return Task.CompletedTask;
        });
    }

    public IDependencyBuilder AddServices(Action<IServiceCollection> services)
    {
        return AddServices((s, _) => services(s));
    }

    public IDependencyBuilder Model<T>()
    {
        _conventions.Add(x => x.Entity<T>());
        return this;
    }

    public IDependencyBuilder JsonModel<T>(Func<T> @default)
    {
        _dbMapping.Add(x => x.DefaultJsonHandler(@default));
        return this;
    }

    public IDependencyBuilder JsonModel<T>() => JsonModel<T?>(() => default);

    public IDependencyBuilder Transient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        return AddServices(x => x.AddTransient<TService, TImplementation>());
    }

    public IDependencyBuilder Singleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        return AddServices(x => x.AddSingleton<TService, TImplementation>());
    }

    public IDependencyBuilder Singleton<TService>(TService instance)
        where TService : class
    {
        return AddServices(x => x.AddSingleton(instance));
    }

    public async Task Build(IServiceCollection services, IConfiguration config)
    {
        static async Task ExecuteFiles(IDbConnection con, string extension)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
            if (!Directory.Exists(path)) return;

            var files = Directory.GetFiles(path, extension, SearchOption.AllDirectories)
                .Where(t => !t.ToLower().EndsWith(".onetime.sql"))
                .OrderBy(t => Path.GetFileName(t))
                .ToArray();

            if (files.Length <= 0) return;

            foreach (var file in files)
            {
                var context = await File.ReadAllTextAsync(file);
                await con.ExecuteAsync(context);
            }
        }

        services
            .AddSqlService(c =>
            {
                c.ConfigureGeneration(a => a.WithCamelCaseChange())
                 .ConfigureTypes(a =>
                 {
                     var conv = a.CamelCase();
                     foreach (var convention in _conventions)
                         convention(conv);

                     foreach (var mapping in _dbMapping)
                         mapping(a);
                 });

                c.AddPostgres<SqlConfig>(a => a.OnInit(con => ExecuteFiles(con, "*.sql")));
            })
            .AddCardboardHttp()
            .AddFileCache()
            .AddSerilog();

        foreach (var action in _services)
            await action(services, config);
    }
}