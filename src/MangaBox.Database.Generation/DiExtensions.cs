using CardboardBox.Setup.CliParser;

namespace MangaBox.Database.Generation;

using Services;
using TypeGeneration;

/// <summary>
/// Extensions for dependency injection
/// </summary>
public static class DiExtensions
{
    /// <summary>
    /// Adds the database generation services
    /// </summary>
    /// <param name="services">The service collection to add to</param>
    /// <returns>The service collection for fluent method chaining</returns>
    public static IServiceCollection AddDatabaseGeneration(this IServiceCollection services)
    {
        return services
            .AddSingleton<ICommentsService, CommentsService>()
            .AddTransient<IFileGenerationService, FileGenerationService>()
            .AddTransient<IDatabaseMetadataService, DatabaseMetadataService>();
    }

    /// <summary>
    /// Adds the database generation verbs
    /// </summary>
    /// <param name="builder">The database generation builder</param>
    /// <returns>The database generation builder for fluent method chaining</returns>
    public static ICommandLineBuilder AddDatabaseGeneration(this ICommandLineBuilder builder)
    {
        return builder
            .Add<GenerateOrmClassesVerb>()
            .Add<GenerateDatabaseScriptsVerb>();
    }

    /// <summary>
    /// Ensures the path uses the correct directory separators
    /// </summary>
    /// <param name="path">The path to convert</param>
    /// <returns>The path with the correct directory separators for the current OS</returns>
    public static string GetOSPath(this string path)
    {
        var osSafe = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return Path.GetFullPath(Path.Combine(osSafe));
    }
}
