using MangaBox.All;
using MangaBox.Cli.Verbs;
using MangaBox.Database.Generation;

var services = new ServiceCollection()
	.AddConfig(c => c
		.AddFile("appsettings.json")
		.AddUserSecrets<Program>(), out var config)
	.AddDatabaseGeneration()
	
	.AddTransient<LegacyPostgresSqlService>();

await services.AddMangaBox(config);

return await services
	.Cli(args, c => c
		.Add<TestVerb>()
		.Add<MigrateVerb>()
		.Add<SetupDbVerb>()
		.Add<ClearImageQueueVerb>()
		.Add<HandleImageQueueVerb>()
		.Add<InitVerb>()
		.AddDatabaseGeneration());