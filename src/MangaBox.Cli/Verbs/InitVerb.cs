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

		_logger.LogInformation("Running command: {Verb}", Verb);
		var service = _providers.GetRequiredService<ICommandLineService>();
		var code = await service.Run(Verb.Split(' '));
		_logger.LogInformation("Command `{Verb}` finished with exit code {ExitCode}", Verb, code);
		return code == ExitCodeSuccess;
	}
}
