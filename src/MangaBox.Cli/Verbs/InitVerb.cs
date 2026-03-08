using CardboardBox.Setup.CliParser;

namespace MangaBox.Cli.Verbs;

[Verb("init", isDefault: true, Hidden = true, HelpText = "Bootstraps the cli")]
internal class InitOptions
{
	
}

internal class InitVerb(
	IConfiguration _config,
	IServiceProvider _providers,
	ILogger<InitVerb> logger) : BooleanVerb<InitOptions>(logger)
{
	public string? Verb => _config["Verb"];

	public override async Task<bool> Execute(InitOptions options, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(Verb))
		{
			_logger.LogError("No verb was provided, cannot initialize");
			return false;
		}

		var service = _providers.GetRequiredService<ICommandLineService>();
		var verbs = Verb.Split("&&", StringSplitOptions.RemoveEmptyEntries)
			.Select(t => t.Trim());
		foreach (var verb in verbs)
		{
			_logger.LogInformation("Running command: {Verb}", verb);
			var code = await service.Run(verb.Split(' '));
			_logger.LogInformation("Command `{Verb}` finished with exit code {ExitCode}", verb, code);

			if (code != ExitCodeSuccess) return false;
		}

		return true;
	}
}
