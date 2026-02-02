using MangaBox.Database;

namespace MangaBox.Cli.Verbs;

[Verb("migrate", HelpText = "Migrates the old CBA manga schema to the new MangaBox schema")]
internal class MigrateOptions { }

internal class MigrateVerb(
	IDbService _db,
	ILogger<MigrateVerb> logger) : BooleanVerb<MigrateOptions>(logger)
{
	public override async Task<bool> Execute(MigrateOptions options, CancellationToken token)
	{
		
		return true;
	}
}