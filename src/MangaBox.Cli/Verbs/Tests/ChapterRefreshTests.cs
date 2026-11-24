using MangaBox.Database;
using MangaBox.Database.Services;
using MangaBox.Models;
using MangaBox.Models.Composites;
using MangaBox.Models.Composites.Import;
using MangaBox.Services;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Checks the loader's forced refresh and replacement-image queue without database or network access.</summary>
internal static class ChapterRefreshTests
{
	/// <summary>Checks unchanged URLs, changed URLs, ordinary reads, and failed replacement handling.</summary>
	/// <param name="token">The cancellation token for loader calls.</param>
	/// <returns>A task that completes after the refresh checks pass.</returns>
	public static async Task Run(CancellationToken token)
	{
		foreach (var changed in new[] { false, true })
		{
			var fixture = new Fixture(changed);
			var oldId = fixture.Image.Id;
			var result = await fixture.Loader.Pages(fixture.Chapter.Id, true, token);
			Require(result.Success && fixture.Image.Id != oldId && fixture.Replacements == 1,
				"Forced page refresh must replace the image ID even when its upstream URL is unchanged.");
			Require(fixture.Published.Count == 1 && fixture.Published[0].Id == fixture.Image.Id &&
				fixture.Published[0].Force == true, "The image queue must receive the replacement ID with forced indexing.");
			Require(fixture.Image.FailedCount == 0 && fixture.Image.LastFailedAt is null,
				"The replacement image must not carry the old image cooldown.");
		}

		var ordinary = new Fixture(false);
		var existingId = ordinary.Image.Id;
		var existing = await ordinary.Loader.Pages(ordinary.Chapter.Id, false, token);
		Require(existing.Success && ordinary.Image.Id == existingId && ordinary.Published.Count == 0 &&
			ordinary.Replacements == 0, "An ordinary chapter read must preserve its image IDs and cache state.");

		var failed = new Fixture(false) { FailReplacement = true };
		var unavailable = await failed.Loader.Pages(failed.Chapter.Id, true, token);
		Require(!unavailable.Success && failed.Published.Count == 0,
			"A failed replacement must return an error without publishing the obsolete image ID.");
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

	private static T Stub<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
	{
		var proxy = DispatchProxy.Create<T, Calls>();
		((Calls)(object)proxy).Handler = handler;
		return proxy;
	}

	private sealed class Fixture
	{
		private readonly MbManga _manga = new() { Id = Guid.NewGuid(), Url = "https://reader.test/title/manga" };
		private readonly string _pageUrl;
		public MbChapter Chapter { get; } = new()
		{
			Id = Guid.NewGuid(), Url = "https://reader.test/title/manga/chapter", PageCount = 1
		};
		public MbImage Image { get; private set; } = new()
		{
			Id = Guid.NewGuid(), Ordinal = 1, Url = "https://cdn.test/page.webp",
			FailedCount = 2, LastFailedAt = DateTime.UtcNow
		};
		public List<QueueImage> Published { get; } = [];
		public int Replacements { get; private set; }
		public bool FailReplacement { get; init; }
		public MangaLoaderService Loader { get; }

		public Fixture(bool changed)
		{
			_pageUrl = changed ? "https://new-cdn.test/page.webp" : Image.Url;
			var chapters = Stub<IMbChapterDbService>((method, _) => method.Name == "FetchWithRelationships"
				? Task.FromResult<MangaBoxType<MbChapter>?>(new(Chapter,
					new() { Type = nameof(MbManga), Data = _manga },
					new() { Type = nameof(MbImage), Data = Image }))
				: throw new InvalidOperationException(method.Name));
			var images = Stub<IMbImageDbService>(ImageCall);
			var db = Stub<IDbService>((method, _) => method.Name switch
			{
				"get_Chapter" => chapters,
				"get_Image" => images,
				_ => throw new InvalidOperationException(method.Name)
			});
			var provider = Stub<IMangaUrlSource>((method, _) => method.Name == "ChapterPages"
				? Task.FromResult<ImportPage[]>([new(_pageUrl)])
				: throw new InvalidOperationException(method.Name));
			var sources = Stub<ISourceService>((method, _) => method.Name == "FindByUrl"
				? Task.FromResult<IdedSource?>(new("manga", new(new MbSource(), provider)))
				: throw new InvalidOperationException(method.Name));
			var queue = Stub<IRedisQueue<QueueImage>>((method, args) =>
			{
				if (method.Name != "Publish")
					throw new InvalidOperationException(method.Name);
				Published.Add((QueueImage)args![0]!);
				return Task.CompletedTask;
			});
			var publisher = Stub<IMangaPublishService>((method, _) => method.Name == "get_NewImages"
				? queue : throw new InvalidOperationException(method.Name));
			Loader = new(db, sources, publisher);
		}

		private object ImageCall(MethodInfo method, object?[]? args)
		{
			if (method.Name == "Upsert")
				return Task.FromResult(Image.Id);
			if (method.Name != "Bust" || (Guid)args![0]! != Image.Id)
				throw new InvalidOperationException(method.Name);

			Replacements++;
			if (FailReplacement)
				return Task.FromResult<MbImage?>(null);

			Image = new() { Id = Guid.NewGuid(), Url = _pageUrl, Ordinal = 1 };
			return Task.FromResult<MbImage?>(Image);
		}
	}

	/// <summary>Routes explicitly allowed in-memory calls and rejects unexpected external dependencies.</summary>
	public class Calls : DispatchProxy
	{
		/// <summary>The allowed contract behavior for this stub.</summary>
		public Func<MethodInfo, object?[]?, object?> Handler { get; set; } =
			(_, _) => throw new InvalidOperationException("Unconfigured dependency call.");

		/// <inheritdoc />
		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
			Handler(targetMethod ?? throw new InvalidOperationException("Missing method."), args);
	}
}
