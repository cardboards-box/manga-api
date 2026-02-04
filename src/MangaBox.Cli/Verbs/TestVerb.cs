namespace MangaBox.Cli.Verbs;

using Database;
using Models;
using Models.Composites;
using Services;

[Verb("test", HelpText = "Run tests.")]
internal class TestOption
{
	[Value(0, Required = true, HelpText = "The test method to run.")]
	public string Method { get; set; } = string.Empty;
}

internal class TestVerb(
	IDbService _db,
	IMangaLoaderService _loader,
	ILogger<TestVerb> logger) : BooleanVerb<TestOption>(logger)
{
	private static readonly JsonSerializerOptions _options = new()
	{
		WriteIndented = true,
		AllowTrailingCommas = true,
	};

	public static string Serialize<T>(T item)
	{
		return JsonSerializer.Serialize(item, _options);
	}

	public void PrintDbMethods()
	{
		string[] skips =
		[
			"Insert",
			"Update",
			"Upsert",
			"Delete",
			"Create",
			"Add",
			"Remove"
		];

		var output = new List<string>();
		var services = _db.GetType().GetProperties();
		foreach (var service in services)
		{
			var methods = service.PropertyType.GetMethods();
			foreach (var method in methods)
			{
				if (skips.Contains(method.Name)) continue;

				var parameters = method.GetParameters();

				var name = $"\"{service.Name}.{method.Name}({string.Join(", ", parameters.Select(t => $"{t.ParameterType.Name} {t.Name}"))})\"";

				var pars = new List<string>();
				foreach (var parameter in parameters)
				{
					var value = parameter.ParameterType switch
					{
						Type t when t == typeof(string) => "\"value\"",
						Type t when t == typeof(int) || t == typeof(long) || t == typeof(short) => "1",
						Type t when t == typeof(Guid) => "Guid.NewGuid()",
						Type t when t == typeof(Guid[]) => "[Guid.NewGuid()]",
						Type t when t == typeof(bool) => "true",
						Type t when t.IsEnum => $"({parameter.ParameterType.Name})0",
						Type t when t == typeof(DateTime) => "DateTime.UtcNow",
						Type t when t == typeof(DateTime?) => "DateTime.UtcNow",
						Type t when t == typeof(CancellationToken) => "CancellationToken.None",
						_ => "default"
					};
					pars.Add(value);
				}

				var invocation = $"() => _db.{service.Name}.{method.Name}({string.Join(", ", pars)})";
				output.Add($"({name}, {invocation})");
			}
		}

		_logger.LogInformation("Output:\r\n{Output}", string.Join(",\r\n", output));
	}

	public async Task UpdateSince()
	{
		var updated = await _db.MangaExt.Update(-0);
		_logger.LogInformation("Updated manga extensions: {Updated}", Serialize(updated));
	}

	public async Task LoadManga(CancellationToken token)
	{
		bool force = false, doLogger = false;
		string[] urls = 
		[
			"https://mangadex.org/title/129c90ca-b997-4789-a748-e8765bc67a65/ichinichi-goto-ni-tsun-ga-hetteku-tsuntsuntsuntsuntsuntsuntsuntsuntsuntsuntsundere-joshi",
			"https://mangadex.org/title/fc0a7b86-992e-4126-b30f-ca04811979bf/the-unrivaled-mememori-kun",
			"https://weebdex.org/title/b1e1fv77hs",
			"https://comix.to/title/772k0-tensei-shitara-ponkotsu-maid-to-yobarete-imashita-zense-no-arekore-wo-mochikomi-wo-yashiki-kaikaku-shimasu",
			"https://mangaclash.com/manga/last-boss-yametemita-shujinkou-ni-taosareta-furi-shite-jiyuu-ni-ikitemita",
			"https://mangakatana.com/manga/the-great-saints-carefree-journey-to-another-world.27345",
			"https://www.natomanga.com/manga/the-great-saint-s-carefree-journey-to-another-world",
			"https://likemanga.in/manga/i-got-my-wish-and-reincarnated-as-the-villainess-last-boss",
			"https://mangadex.org/title/b3a9c1f8-93d2-49ba-96e2-84727c1031a6/isekai-ni-otosareta-jouka-wa-kihon"
		];

		var profileId = (await _db.Profile.Admins()).FirstOrDefault()?.Id;

		await Parallel.ForEachAsync(urls, token, async (url, token) =>
		{
			var result = await _loader.Load(profileId, url, force, token);
			if (result is not Boxed<MangaBoxType<MbManga>> manga)
			{
				_logger.LogWarning("Failed to load manga for {URL}: {Result}", url, Serialize(result));
				return;
			}

			if (doLogger)
				_logger.LogInformation("Result: {Result}", Serialize(manga));

			var mid = manga.Data?.Entity.Id;
			if (!mid.HasValue)
			{
				_logger.LogWarning("Manga ID is null for {URL}: {Result}", url, Serialize(result));
				return;
			}

			var chapters = await _db.Chapter.ByManga(mid.Value);
			if (chapters.Length == 0)
			{
				_logger.LogWarning("No chapters found for manga ID {MangaId}", mid.Value);
				return;
			}

			if (doLogger)
				_logger.LogInformation("Chapters: {Chapters}", Serialize(chapters));

			var pages = await _loader.Pages(chapters.First().Id, force, token);
			if (pages is not Boxed<MangaBoxType<MbChapter>> fullChapter)
			{
				_logger.LogWarning("Failed to load chapter pages for {ChapterId}: {Result}", chapters.First().Id, Serialize(pages));
				return;
			}

			_logger.LogInformation("Pages: {Pages}", fullChapter.Data?.GetItems<MbImage>()?.Count() ?? -1);
		});
	}

	public override async Task<bool> Execute(TestOption options, CancellationToken token)
	{
		var methods = GetType().GetMethods();
		var method = methods.FirstOrDefault(t => t.Name.EqualsIc(options.Method));

		if (method is null)
		{
			_logger.LogError("The method {Method} does not exist", options.Method);
			return false;
		}

		object[] parameters = method.GetParameters().Length <= 0 ? [] : [token];
		var result = method.Invoke(this, parameters);
		if (result is null) { }
		else if (result is Task task)
			await task;
		else if (result is ValueTask vTask)
			await vTask;

		_logger.LogInformation("Method execution complete");
		return true;
	}
}
