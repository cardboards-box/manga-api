namespace MangaBox.Cli.Verbs;

using Database;
using Models;
using Models.Composites;
using Providers.Sources;
using Services;
using Services.Imaging;
using Utilities.Comix;
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
	IHttpService _http,
	ISourceService _sources,
	IHyakuroSource _hyakuro,
	IKappaBeastSource _kappa,
	ILilyMangaSource _lily,
	IMangaFireSource _mangaFire,
	IMangaReadSource _mangaRead,
	INhentaiNetSource _nhentaiNet,
	IHentaiNexusSource _hentaiNexus,
	IFlareImageService _flare,
	IMangaLoaderService _loader,
	IRestitcherService _restitch,
	IFlareSolverService _flareHtml,
	IProxiedHttpService _proxied,
	IComixWAFService _comixWaf,
    IPortainerService _portainer,
    IOptions<PortainerOptions> _portainerOpts,
    ILogger<TestVerb> logger) : BooleanVerb<TestOption>(logger)
{
	private static readonly JsonSerializerOptions _options = new()
	{
		WriteIndented = true,
		AllowTrailingCommas = true,
	};

	public static string Serialize<T>(T item)
	{
		return JsonSerializer.Serialize(item, item!.GetType(), _options);
	}

	private void LogHeader(DownloadResult result, string name)
	{
		if (result.Response is null)
			return;

		var value = result.Response.Headers.TryGetValues(name, out var values)
			? values.FirstOrDefault()
			: result.Response.Content.Headers.TryGetValues(name, out values)
				? values.FirstOrDefault()
				: null;

		if (!string.IsNullOrWhiteSpace(value))
			_logger.LogInformation("{Header}: {Value}", name, value);
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

	public Task TestHentaiNexus(CancellationToken token)
	{
		const string URL = "https://hentainexus.com/view/5297";
		return TestSource(_hentaiNexus, URL, true, token);
	}

	public async Task TestHentaiNexusSearch(CancellationToken token)
	{
		const string EXPECTED_URL = "https://hentainexus.com/view/5297";
		HentaiNexusQuery[] query =
		[
			new("artist", "Aomushi"),
			new("tag", "creampie")
		];
		var queryString = string.Join(" ", query.Select(x => x.ToString()));
		var pageOne = await _hentaiNexus.Search(query, 1, token);
		var pageTwo = await _hentaiNexus.Search(query, 2, token);
		var all = await _hentaiNexus.Search(query, token);

		_logger.LogInformation(
			"HentaiNexus search results for {Query}: page1={PageOneCount}, page2={PageTwoCount}, total={TotalCount}, urls={Urls}",
			queryString,
			pageOne.Length,
			pageTwo.Length,
			all.Length,
			Serialize(all));

		if (all.Length == 0)
			_logger.LogWarning("No HentaiNexus search results found for query: {Query}", queryString);

		if (!all.Contains(EXPECTED_URL, StringComparer.OrdinalIgnoreCase))
			_logger.LogWarning("Expected HentaiNexus search result was not found: {Url}", EXPECTED_URL);
	}

	public Task TestComix(CancellationToken token)
	{
		Task BasicTest(CancellationToken token)
		{
			const string URL = "https://comix.to/title/e93mr-tensei-youjo-wa-owabi-cheat-de-isekai-going-my-way";
			return TestSource(_comix, URL, true, token, null);
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

	public async Task TestSource(IMangaSource source, string url, bool images, CancellationToken token, int? maxImages = 10)
	{
		const string DIR = "test-source";

		if (!Directory.Exists(DIR))
            Directory.CreateDirectory(DIR);

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
		var downloadPages = maxImages is null
			? pages
			: pages.Take(maxImages.Value);

		var downloadDir = Path.Combine(DIR, name);
		if (!Directory.Exists(downloadDir))
			Directory.CreateDirectory(downloadDir);

		await Parallel.ForEachAsync(downloadPages, opts, async (page, token) =>
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
				var path = Path.Combine(downloadDir, name);
				using var io = File.Create(path);
				await image.Stream.CopyToAsync(io, token);
				await io.FlushAsync(token);

				_logger.LogInformation("Successfully downloaded page {PageUrl} of chapter ID: {ChapterId} of manga ID: {ID} >> {Name}", 
					page.Page, chapter.Id, id, path);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing page {PageUrl} of chapter ID: {ChapterId} of manga ID: {ID}", page.Page, chapter.Id, id);
				return;
			}
		});
	}

	public async Task TestSource(string url, bool dlImages, CancellationToken token, int? maxImages = 10)
	{
        const string DIR = "test-source";

        if (!Directory.Exists(DIR))
            Directory.CreateDirectory(DIR);

		var load = await _loader.Load(null, url, true, token);
		_logger.LogInformation("Load Results: {Results}", Serialize(load));
		if (load.IsError<MangaBoxType<MbManga>>(out var error, out var data))
		{
			_logger.LogError("Error occurred while loading {Url} >> {error}", url, error);
			return;
		}

		var chapters = await _db.Chapter.ByManga(data.Entity.Id);
		if (chapters.Length == 0)
		{
			_logger.LogError("No chapters found for manga: {title}", data.Entity.Title);
			return;
		}

		var fp = chapters.OrderBy(t => t.Ordinal).First();
		var pages = await _loader.Pages(fp.Id, false, token);
		if (pages.IsError<MangaBoxType<MbChapter>>(out error, out var chapter))
		{
			_logger.LogError("Failed to fetch chapter: {Id} >> {Title}", fp.Id, data.Entity.Title);
			return;
		}


        var images = chapter.GetItems<MbImage>().OrderBy(t => t.Ordinal).ToArray();
		if (images.Length == 0)
		{
			_logger.LogError("No images found for chapter: {Id} >> {title}", chapter.Entity.Id, chapter.Entity.Title);
			return;
		}

		if (!dlImages) return;

		var dlDir = Path.Combine(DIR, chapter.Entity.Id.ToString());
		if (!Directory.Exists(dlDir))
			Directory.CreateDirectory(dlDir);

		images = maxImages is null ? images : [..images.Take(maxImages.Value)];

        var opts = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = token
        };
		await Parallel.ForEachAsync(images, opts, async (i, c) =>
		{
			using var image = await _image.Get(i.Id, c);
            if (!string.IsNullOrEmpty(image.Error) || image.Stream is null)
            {
                _logger.LogError("Error occurred while fetching image: {Error} >> {Page}", image.Error, i.Url);
                return;
            }

            var name = image.FileName ?? (i.Url.MD5Hash() + ".jpg");
            var path = Path.Combine(dlDir, name);
            using var io = File.Create(path);
            await image.Stream.CopyToAsync(io, token);
            await io.FlushAsync(token);

            _logger.LogInformation("Successfully downloaded page {PageUrl} of chapter ID: {ChapterId} of manga ID: {ID} >> {Name}",
                i.Url, chapter.Entity.Id, data.Entity.Title, path);
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
		async Task<bool> BustImages(Guid chapterId)
		{
			var images = await _db.Chapter.FetchWithRelationships(chapterId);
			if (images is null)
			{
				_logger.LogWarning("No images found for chapter ID: {ChapterId}", chapterId);
				return false;
			}

			var imageIds = images.GetItems<MbImage>()?.Select(t => t.Id).ToHashSet();
            if (imageIds is null || imageIds.Count == 0) 
			{ 
				_logger.LogWarning("Nothing to bust for chapter ID: {ChapterId}", chapterId);
                return false; 
			}

            var opts = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 5)
            };
            var failed = 0;
            await Parallel.ForEachAsync(imageIds, opts, async (id, ct) =>
            {
                var result = await _image.Bust(id, ct);
                if (result.Success) return;

                Interlocked.Increment(ref failed);
            });

			_logger.LogInformation("Bust operation completed for chapter ID: {ChapterId}. Failed attempts: {Failed}", chapterId, failed);
            return failed > 0;
        }

		var id = Guid.Parse("5e4520c7-2ae8-4965-8622-660bdecf35b7");
		await BustImages(id);
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
		const string CACHE = "file-cache";
		string[] IMAGE_IDS =
		[
            "32a2d837-7afa-4b16-aaae-d6c1c0e8427b",
		];

		if (Directory.Exists(CACHE))
			Directory.Delete(CACHE, true);

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

	public async Task TestComixImage(CancellationToken token)
	{
		async Task TestImage(string url, CancellationToken token)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				_logger.LogError("Usage: test TestComixImage <image-url>");
				return;
			}

			const string DIR = "test-comix-images";
			if (!Directory.Exists(DIR))
				Directory.CreateDirectory(DIR);

			var loader = await _sources.FindBySlug(_comix.Provider, token);
			if (loader is null)
			{
				_logger.LogError("Comix source was not found or is disabled");
				return;
			}

			var manga = new MbManga
			{
				Id = Guid.NewGuid(),
				SourceId = loader.Info.Id,
				Referer = loader.Info.Referer,
				UserAgent = loader.Info.UserAgent,
			};
			var image = new MbImage
			{
				Id = Guid.NewGuid(),
				MangaId = manga.Id,
				ChapterId = Guid.NewGuid(),
				Url = url,
			};

			var headers = _http.HeadersFrom(url, loader.Info, manga, image);
			IDownloadService downloader = loader.Service.UseFlareImages
				? _flare
				: loader.Service.UseProxiedImages
					? _proxied
					: _http;

			_logger.LogInformation("Downloading Comix image with {Downloader}: {Url}", downloader.GetType().Name, url);
			using var download = await loader.Service.DownloadImage(downloader, url, headers, token);
			if (!string.IsNullOrEmpty(download.Error) || download.Stream is null)
			{
				_logger.LogError("Error occurred while fetching image: {Error} >> {Url}", download.Error, url);
				return;
			}

			LogHeader(download, "x-enc-seed");
			LogHeader(download, "x-enc-len");
			LogHeader(download, "x-enc-algo");
			LogHeader(download, "x-scramble-seed");
			LogHeader(download, "x-scramble-grid");
			LogHeader(download, "x-scramble-algo");
			LogHeader(download, "x-scramble-hash");

			var stem = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{url.MD5Hash()}";
			var extension = Path.GetExtension(download.FileName)?.TrimStart('.');
			if (string.IsNullOrWhiteSpace(extension))
				extension = _http.DetermineExtension(download.MimeType);
			if (string.IsNullOrWhiteSpace(extension))
				extension = "dat";

			var rawPath = Path.Combine(DIR, $"{stem}.raw.{extension}");
			var outputPath = Path.Combine(DIR, $"{stem}.processed.{extension}");

			await using (var io = File.Create(rawPath))
			{
				await download.Stream.CopyToAsync(io, token);
				await io.FlushAsync(token);
			}

			File.Copy(rawPath, outputPath, true);
			await loader.Service.PostProcessDownload(download, outputPath, token);

			var (width, height, loaded) = await _http.DetermineImageSize(outputPath);
			_logger.LogInformation("Downloaded Comix image: raw={RawPath}, decoded={OutputPath}, size={Width}x{Height}",
				rawPath, outputPath, width, height);

			using var sourceImage = loaded;
			if (sourceImage is null)
			{
				_logger.LogWarning("Downloaded file could not be decoded as an image after download post-processing: {Path}", outputPath);
				return;
			}

			using var processed = await loader.Service.PostProcessing(download, sourceImage, token);
			if (processed is null)
			{
				_logger.LogInformation("Comix image did not require SkiaSharp post-processing: {Path}", outputPath);
				return;
			}

			var finalPath = Path.Combine(DIR, $"{stem}.final.{extension}");
			await using (var io = File.Create(finalPath))
			{
				await SkiaImageHelpers.SaveAsync(processed, io, SkiaImageHelpers.DetermineFormat(finalPath, download.MimeType), token);
				await io.FlushAsync(token);
			}

			_logger.LogInformation("Comix ImageService-style output written to {Path}", finalPath);
		}

		string[] urls =
		[
			"https://ek10.wowpic1.store/i5/bEqPbYfoMT0Gm13lbl6foA5A3oUJbu6i3R0VvpbI6y4EiS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4AjS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4AhS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4AgS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4ImS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4InS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4IkS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4IlS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4IqS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4IrS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4MiS5FIHyEz7PI11FmpSw",
			"https://ek10.wowpic4.store/i5/bEqPbYfoNT0GmyHlFi6foAJozoUFavqi3R0VvpbI6y4MjS5FIHyEz7PI11FmpSw",
		];

		await Parallel.ForEachAsync(urls, token, async (url, token) =>
		{
			await TestImage(url, token);
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

	public async Task TestComixWafRotation(CancellationToken token)
	{
		const string TEST = @"{
    ""captcha_id"": ""fff241711cc0cd1a8ac78190216d3fa4"",
    ""image_base64"": ""data:image/png;base64,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"",
    ""thumb_base64"": ""data:image/png;base64,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"",
    ""count"": 0,
    ""thumb_size"": 160
}";
		const int SHOULD_BE = 110;

		var waf = JsonSerializer.Deserialize<ComixWAFGenerate>(TEST);
		if (waf is null)
		{
			_logger.LogWarning("Failed to deserialize ComixWAFGenerate");
			return;
        }

		var result = _comixWaf.GetRotation(waf);
		_logger.LogInformation("Waf Result: {Result}, Should be {Should}, yes? {Yes}", result, SHOULD_BE, (int)result == SHOULD_BE);
    }

	public async Task TestRefresh(CancellationToken token)
	{
		const string ID = "b31b3c52-6fdd-419f-be9e-6b5ee3acfda5";
		var result = await _loader.Refresh(null, Guid.Parse(ID), token);
		_logger.LogInformation("Refresh finished: {State}", Serialize(result));
    }

	public Task TestLoad(CancellationToken token)
	{
		const string URL = "https://comix.to/title/e93mr-tensei-youjo-wa-owabi-cheat-de-isekai-going-my-way";
		return TestSource(URL, true, token);
    }

    public async Task TestRestarts(CancellationToken token)
    {
        _logger.LogInformation("Container options: {Containers}", Serialize(_portainerOpts.Value));

        var container = _portainerOpts.Value.Containers.FirstOrDefault();
        if (container is null)
        {
            _logger.LogError("No container found in Portainer options");
            return;
        }

		var state = await _portainer.State(container, token);
        _logger.LogInformation("Container {ContainerName} state: {State}", container.Container, Serialize(state));

        _logger.LogInformation("Restarting container {ContainerName}", container.Container);
        var result = await _portainer.Restart(container, token);
        _logger.LogInformation("Restart result: {Result}", result);
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

		object[] parameters = [..method.GetParameters().Select(parameter =>
		{
			if (parameter.ParameterType == typeof(CancellationToken))
				return (object)token;
			if (parameter.ParameterType == typeof(TestOption))
				return options;

			return parameter.HasDefaultValue ? parameter.DefaultValue! : null!;
		})];
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
