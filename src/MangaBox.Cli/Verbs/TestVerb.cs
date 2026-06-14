namespace MangaBox.Cli.Verbs;

using Database;
using Models;
using Models.Composites;
using Providers.Sources;
using Services;
using Services.Imaging;
using Utilities.Flare;

[Verb("test", HelpText = "Run tests.")]
internal class TestOption
{
	[Value(0, Required = true, HelpText = "The test method to run.")]
	public string Method { get; set; } = string.Empty;
}

internal class TestVerb(
	IDbService _db,
	IComixSource _comix,
	IMangaDexSource _md,
	IImageService _image,
	ISourceService _sources,
	IHyakuroSource _hyakuro,
	IKappaBeastSource _kappa,
	ILilyMangaSource _lily,
	IMangaFireSource _mangaFire,
	IMangaReadSource _mangaRead,
	INhentaiNetSource _nhentaiNet,
	IFlareImageService _flare,
	IMangaLoaderService _loader,
	IRestitcherService _restitch,
	IFlareSolverService _flareHtml,
	IProxiedHttpService _proxied,
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

	public Task TestLilyManga(CancellationToken token)
	{
		const string URL = "https://lilymanga.net/gl/shino-to-ren/";
		return TestSource(_lily, URL, false, token);
	}

	public Task TestMangaRead(CancellationToken token)
	{
		const string URL = "https://www.mangaread.org/manga/martial-peak/";
		return TestSource(_mangaRead, URL, false, token);
	}

	public Task TestMangaFire(CancellationToken token)
	{
		const string URL = "https://mangafire.to/manga/koushaku-ke-no-aisare-nise-youjo.82nov";
		return TestSource(_mangaFire, URL, false, token);
	}

	public Task TestNhentaiNet(CancellationToken token)
	{
		const string URL = "https://nhentai.net/g/655680/";
		return TestSource(_nhentaiNet, URL, true, token);
	}

	public async Task TestNhentaiNetSearch(CancellationToken token)
	{
		NhentaiNetQuery[] query =
		[
			new("artist", "yokoya manjirou"),
			new("tag", "yaoi", true),
			new("language", "english")
		];
		var str = string.Join(" ", query.Select(t => t.ToString()));
		var results = await _nhentaiNet.Search(query, 1, token);
		_logger.LogInformation("NHentai.net search results for {Query}: {Results}", str, Serialize(results));

		if (results.Length == 0)
			_logger.LogWarning("No NHentai.net search results found for query: {Query}", str);
	}

	public Task TestComix(CancellationToken token)
	{
		Task BasicTest(CancellationToken token)
		{
			const string URL = "https://comix.to/title/vvnqy-mangatitle";//"https://comix.to/title/8w6dm-i-saved-you-but-im-not-responsible";
			return TestSource(_comix, URL, true, token);
		}

		async Task DebugChapters(CancellationToken token)
		{
			const string DIR = "debug";
			string[] urls =
			[
				"https://comix.to/title/yrqn-mangatitle/8884381-chapter-1",
				"https://comix.to/title/vvnqy-mangatitle/5634451-chapter-1"
			];

			if (!Directory.Exists(DIR))
				Directory.CreateDirectory(DIR);

			await using var session = await _flareHtml.CreateSession(null, token);
			var instance = new FlareSolverInstance(session, _logger)
			{
				MaxRequestsBeforePauseMin = 5,
				MaxRequestsBeforePauseMax = 15,
				ResponseWait = TimeSpan.FromSeconds(5),
				DisableMedia = false,
			};

			for (var i = 0; i < urls.Length; i++)
			{
				var url = urls[i];
				var doc = await instance.GetHtml(url, token);

				await File.WriteAllTextAsync($"{DIR}/debug-{i}.html", doc.FlareSolution.Response, token);
				var result = JsonSerializer.Serialize(doc.FlareSolution, _options);
				await File.WriteAllTextAsync($"{DIR}/debug-{i}.json", result, token);
			}
		}

		////const string URL = "https://comix.to/title/60jxz-tsuihou-saikyou-kuzu-kenja-no-henkyou-kosodate-slow-life-kuzu-da-to-kanchigaisaregachi-na-saikyou-no-zennin-wa-maou-no-musume-wo-chouzetsu-iko-ni-sodateageru";
		return BasicTest(token);
	}

	public async Task LoadManga(CancellationToken token)
	{
		bool force = true, doLogger = false;
		string[] urls = 
		[
			"https://mangadex.org/title/85b3504c-62e8-49e7-9a81-fb64a3f51def",
			//"https://mangadex.org/title/129c90ca-b997-4789-a748-e8765bc67a65/ichinichi-goto-ni-tsun-ga-hetteku-tsuntsuntsuntsuntsuntsuntsuntsuntsuntsuntsundere-joshi",
			//"https://mangadex.org/title/fc0a7b86-992e-4126-b30f-ca04811979bf/the-unrivaled-mememori-kun",
			//"https://weebdex.org/title/b1e1fv77hs",
			//"https://comix.to/title/772k0-tensei-shitara-ponkotsu-maid-to-yobarete-imashita-zense-no-arekore-wo-mochikomi-wo-yashiki-kaikaku-shimasu",
			//"https://mangaclash.com/manga/last-boss-yametemita-shujinkou-ni-taosareta-furi-shite-jiyuu-ni-ikitemita",
			//"https://mangakatana.com/manga/the-great-saints-carefree-journey-to-another-world.27345",
			//"https://www.natomanga.com/manga/the-great-saint-s-carefree-journey-to-another-world",
			//"https://likemanga.in/manga/i-got-my-wish-and-reincarnated-as-the-villainess-last-boss",
			//"https://mangadex.org/title/b3a9c1f8-93d2-49ba-96e2-84727c1031a6/isekai-ni-otosareta-jouka-wa-kihon"
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
		await Parallel.ForEachAsync(pages.Take(10), async (page, token) =>
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

	public async Task TestRestitch(CancellationToken token)
	{
		static (Guid id, int start, int end) Slice(bool first, int start, int end)
		{
			Guid i1 = Guid.Parse("0c770d8d-6561-4751-a9df-17d700cbf628"),
				 i2 = Guid.Parse("0a32a5b6-f21a-49fd-8141-fb2eaa0d2573");
			return (first ? i1 : i2, start, end);
		}

		static IEnumerable<(Guid id, int start, int end)> SliceMany(bool first, int start, params int[] coords)
		{
			int last = start;
			foreach(var coord in coords)
			{
				yield return Slice(first, last, coord);
				last = coord;
			}
		}

		var firstImage = SliceMany(true, 0, 472, 1607, 2743, 3880, 4980, 6152, 7288)
			.Select((t, i) => new ImageSliceImage(i + 1, [new(1, t.id, t.start, t.end)]))
			.ToArray();

		var fs = Slice(true, 7289, 7349);
		var ss = Slice(false, 0, 1075);
		var inter = new ImageSliceImage(firstImage.Length + 1,
			[
				new(1, fs.id, fs.start, fs.end),
				new(2, ss.id, ss.start, ss.end)
			]);

		var secondImage = SliceMany(false, 1076, 2211, 3346, 4483, 5619, 6755, 7348)
			.Select((t, i) => new ImageSliceImage(inter.Ordinal + i + 1, [new(1, t.id, t.start, t.end)]))
			.ToArray();

		var request = new ImageRestitchRequest(
			Guid.Parse("8e5a47be-5718-444e-b68c-74dccb223823"),
			[
				..firstImage,
				inter,
				..secondImage
			]);

		var resp = await _restitch.Restitch(request, token);
		_logger.LogInformation("Restitch response: {Response}", Serialize(resp));
	}

	public async Task TestRestitcher(CancellationToken token)
	{
		var id = Guid.Parse("5e4520c7-2ae8-4965-8622-660bdecf35b7");
		var result = await _image.Download(id, Services.CBZModels.ComicFormat.Zip, token);
		if (!string.IsNullOrEmpty(result.Error) || result.Stream is null)
		{
			_logger.LogError("Error occurred while fetching image: {Error} >> {ID}", result.Error, id);
			return;
		}

		using var io = File.Create("restitcher-test.zip");
		await result.Stream.CopyToAsync(io, token);
		await io.FlushAsync(token);

		_logger.LogInformation("Successfully downloaded image with ID: {ID} >> restitcher-test.zip", id);
	}

	public async Task TestImages(CancellationToken token)
	{
		const string DIR = "test-images";
		string[] IMAGE_IDS =
		[
			"32b27664-c180-47e6-a563-a53e7b7aea27"
		];

		if (!Directory.Exists(DIR))
			Directory.CreateDirectory(DIR);

		var opt = new ParallelOptions
		{
			CancellationToken = token,
			MaxDegreeOfParallelism = 4
		};

		await Parallel.ForEachAsync(IMAGE_IDS, opt, async (id, token) =>
		{
			using var image = await _image.Get(Guid.Parse(id), token);
			if (!string.IsNullOrEmpty(image.Error) || image.Stream is null)
			{
				_logger.LogError("Error occurred while fetching image: {Error} >> {ID}", image.Error, id);
				return;
			}

			var name = image.FileName ?? (id + ".jpg");
			var path = Path.Combine(DIR, name);
			using var io = File.Create(path);
			await image.Stream.CopyToAsync(io, token);
			await io.FlushAsync(token);
			_logger.LogInformation("Successfully downloaded image with ID: {ID} >> {Path}", id, path);
		});
	}

	public Task TestKappaBeast(CancellationToken token)
	{
		const string URL = "https://kappabeast.com/series/jimoto-no-ijimekko-tachi-ni-shikaeshi-shiyou-to-shitara-betsu-no-tatakai-ga-hajimatta";
		return TestSource(_kappa, URL, true, token);
	}

	public async Task TestMdIndexing(CancellationToken token)
	{
		var source = await _sources.FindBySlug("mangadex", token);
		if (source is null)
		{
			_logger.LogError("MangaDex source not found");
			return;
		}

		var results = await _md.Index(source, token).ToArrayAsync(token);
		foreach (var result in results)
		{
			_logger.LogInformation("Indexed manga: {Manga}", Serialize(result));
		}
	}

	public async Task TestProxies(CancellationToken token)
	{
		const string URL = "https://t2.nhentai.net/galleries/3975529/cover.webp.webp";
		const int REQUESTS = 10;
		const string DIR = "proxy-tests";

		if (!Directory.Exists(DIR))
			Directory.CreateDirectory(DIR);

		var opts = new ParallelOptions
		{
			CancellationToken = token,
			MaxDegreeOfParallelism = Environment.ProcessorCount,
		};

		await Parallel.ForEachAsync(Enumerable.Range(0, REQUESTS), opts, async (i, token) =>
		{
			try
			{
				using var result = await _proxied.Download(URL, null, token);
				if (!string.IsNullOrEmpty(result.Error) || result.Stream is null)
				{
					_logger.LogError("Error occurred while fetching URL: {Error} >> {URL}", result.Error, URL);
					return;
				}

				var name = $"proxy-test-{i + 1}.{Path.GetExtension(URL)?.TrimStart('.') ?? "dat"}";
				var path = Path.Combine(DIR, name);
				using var io = File.Create(path);
				await result.Stream.CopyToAsync(io, token);
				await io.FlushAsync(token);
				_logger.LogInformation("Response for request {RequestNumber}: {Path}", i + 1, path);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during proxy test request {RequestNumber} for URL: {URL}", i + 1, URL);
			}
		});
	}

	public async Task TestProxies2(CancellationToken token)
	{
		const string PROXY_HOSTNAME = "localhost";
		int[] PROXY_PORTS = [3300, 3301, 3302];

		var proxies = PROXY_PORTS.Select(t => $"socks5://{PROXY_HOSTNAME}:{t}").ToArray();
		const string DIR = "proxy-tests";

		const string URL = "https://t2.nhentai.net/galleries/3975529/cover.webp.webp";

		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount,
			CancellationToken = token
		};

		int index = 0;

		await Parallel.ForEachAsync(proxies, opts, async (proxyUrl, token) =>
		{
			try
			{
				int i = index;
				Interlocked.Increment(ref index);

				var proxy = new WebProxy
				{
					Address = new Uri(proxyUrl),
					BypassProxyOnLocal = false,
					UseDefaultCredentials = false,
				};
				var handler = new HttpClientHandler
				{
					Proxy = proxy,
					UseProxy = true,
				};
				using var client = new HttpClient(handler);
				using var response = await client.GetAsync(URL, token);

				var name = $"proxy-test-{i + 1}.{Path.GetExtension(URL)?.TrimStart('.') ?? "dat"}";
				var path = Path.Combine(DIR, name);
				using var io = File.Create(path);
				await response.Content.CopyToAsync(io, token);
				await io.FlushAsync(token);
				_logger.LogInformation("Response for request {RequestNumber}: {Path}", i + 1, path);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while testing proxy: {Proxy} for URL: {URL}", proxyUrl, URL);
			}
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
