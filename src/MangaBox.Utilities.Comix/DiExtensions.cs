namespace MangaBox.Utilities.Comix;

/// <summary>
/// Extension methods for dependency injection
/// </summary>
public static class DiExtensions
{
    /// <summary>
    /// Adds the comix services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for fluent method chaining</returns>
    public static IServiceCollection AddComix(this IServiceCollection services)
    {
        return services
            .AddTransient<IComixWAFService, ComixWAFService>()
            .AddTransient<IComixHtmlService, ComixHtmlService>();
    }

    /// <summary>
    /// The headers for Comix requests
    /// </summary>
    /// <param name="referer">The referer</param>
    /// <param name="userAgent">The user-agent</param>
    /// <returns>The standard headers</returns>
    internal static Dictionary<string, string[]> ComixBaseHeaders(string referer, string userAgent)
    {
        var os = userAgent.ContainsIc("Linux") ? "Linux" : "Windows";
        var version = userAgent.ContainsIc("Chrome/") ? userAgent.Split("Chrome/")[1].Split(' ')[0] : "Unknown";
        var verFirst = version.Split('.').First();

        return new()
        {
            ["cache-control"] = ["no-cache"],
            ["pragma"] = ["no-cache"],
            ["priority"] = ["u=1, i"],
            ["referer"] = [referer],
            ["origin"] = ["https://comix.to"],
            ["sec-ch-ua"] = ["\"Not=A?Brand\"", "v=\"99\", \"Google Chrome\"", $"v=\"{verFirst}\", \"Chromium\"", $"v=\"{verFirst}\""],
            ["sec-ch-ua-arch"] = ["\"x86\""],
            ["sec-ch-ua-bitness"] = ["\"64\""],
            ["sec-ch-ua-full-version"] = [$"\"{version}\""],
            ["sec-ch-ua-mobile"] = ["?0"],
            ["sec-ch-ua-model"] = ["\"\""],
            ["sec-ch-ua-platform"] = [$"\"{os}\""],
            ["sec-ch-ua-platform-version"] = ["\"19.0.0\""],
            ["sec-fetch-dest"] = ["empty"],
            ["sec-fetch-mode"] = ["cors"],
            ["sec-fetch-site"] = ["same-origin"],
            ["user-agent"] = [userAgent],
        };
    }
}
