namespace MangaBox.Cli.Verbs;

using Database;

[Verb("setup", HelpText = "Sets up the MangaBox database schema")]
internal class SetupDbOptions { }

internal class SetupDbVerb(
	IDbService _db,
	ILogger<SetupDbVerb> logger) : BooleanVerb<SetupDbOptions>(logger)
{
	public override async Task<bool> Execute(SetupDbOptions options, CancellationToken token)
	{
		await _db.MangaExt.MassUpdate();
		return true;
	}
}
