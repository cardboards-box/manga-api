namespace MangaBox.Cli.Verbs;

using Database;
using Models;
using Models.Composites;
using Providers.Sources;
using Services;
using Services.Imaging;

[Verb("test", HelpText = "Run tests.")]
internal class TestOption
{
	[Value(0, Required = true, HelpText = "The test method to run.")]
	public string Method { get; set; } = string.Empty;
}

internal class TestVerb(
	IDbService _db,
	IComixSource _comix,
	IHyakuroSource _hyakuro,
	IFlareImageService _flare,
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

	public Task TestHyakuro(CancellationToken token)
	{
		const string URL = "https://hyakuro.net/manga/boku-wa-kimitachi-wo-shihai-suru";
		return TestSource(_hyakuro, URL, false, token);
	}

	public Task TestComix(CancellationToken token)
	{
		const string URL = "https://comix.to/title/305e0-i-was-trapped-in-a-dungeon-for-25-years-and-became-a-legendary-suspicious-person-when-rescued";
		return TestSource(_comix, URL, true, token);
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

	public async Task TestSource(IMangaSource source, string url, bool images, CancellationToken token)
	{
		var name = source.Name;
		var (match, id) = source.MatchesProvider(url);
		if (!match || string.IsNullOrEmpty(id))
		{
			_logger.LogError("URL does not match {Name} provider: {URL}", name, url);
			return;
		}

		var manga = await source.Manga(id, token);
		if (manga is null)
		{
			_logger.LogError("Failed to fetch manga from {Name} for ID: {ID}", name, id);
			return;
		}

		_logger.LogInformation("Fetched manga from {Name}: {Manga}", name, Serialize(manga));

		var chapter = manga.Chapters.FirstOrDefault();
		if (chapter is null)
		{
			_logger.LogError("No chapters found for manga ID: {ID} from {Name}", id, name);
			return;
		}

		var pages = await source.ChapterPages(id, chapter.Id, token);
		if (pages.Length == 0)
		{
			_logger.LogError("No pages found for chapter ID: {ChapterId} of manga ID: {ID}", chapter.Id, id);
			return;
		}

		_logger.LogInformation("Fetched {PageCount} pages for chapter ID: {ChapterId} of manga ID: {ID}", pages.Length, chapter.Id, id);

		if (!images) return;

		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = 4,
			CancellationToken = token
		};
		await Parallel.ForEachAsync(pages, async (page, token) =>
		{
			try
			{
				using var image = await _flare.Download(page.Page, null, token);
				if (!string.IsNullOrEmpty(image.Error) || image.Stream is null)
				{
					_logger.LogError("Error occurred while fetching image: {Error} >> {Page}", image.Error, page.Page);
					return;
				}

				var name = image.FileName ?? (page.Page.MD5Hash() + ".jpg");
				using var io = File.Create(name);
				await image.Stream.CopyToAsync(io, token);
				await io.FlushAsync(token);

				_logger.LogInformation("Successfully downloaded page {PageUrl} of chapter ID: {ChapterId} of manga ID: {ID} >> {Name}", 
					page.Page, chapter.Id, id, name);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing page {PageUrl} of chapter ID: {ChapterId} of manga ID: {ID}", page.Page, chapter.Id, id);
				return;
			}
		});
	}

	public async Task TestZeroPages()
	{
		var items = await _db.Chapter.GetZeroPageChapters();
		_logger.LogInformation("Chapters with zero pages: {Count}", items.Length);
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
