using MangaBox.Cli.Verbs;
using MangaBox.Database;
using MangaBox.Database.Generation;
using MangaBox.Jwt;
using MangaBox.Services;
using MangaBox.Providers;

var services = new ServiceCollection()
	.AddConfig(c => c
		.AddFile("appsettings.json")
		.AddUserSecrets<Program>(), out var config)
	.AddDatabaseGeneration()
	.AddJwt()
	.AddCoreServices()
	.AddGeneralServices()
	.AddSources();

await services.AddServices(config, c => c.AddDatabase());

return await services
	.Cli(args, c => c
		.Add<TestVerb>()
		.Add<MigrateVerb>()
		.AddDatabaseGeneration());