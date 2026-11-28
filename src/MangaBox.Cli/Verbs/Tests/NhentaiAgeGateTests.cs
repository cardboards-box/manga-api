using MangaBox.Database;
using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;
using MangaBox.Utilities.Flare;
using MangaBox.Utilities.Flare.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Exercises age confirmation through the real provider and solver retry loop without external access.</summary>
internal static class NhentaiAgeGateTests
{
	private const string URL = "https://nhentai.net/g/42/";
	private const string AGE_GATE = "<form><input id='age-month'><input id='age-day'><input id='age-year'></form>";
	private const string GALLERY = """
		<div id="info"><h1>Example gallery</h1></div>
		<div class="thumb-container"><img src="https://t1.nhentai.net/galleries/123/1t.jpg"></div>
		<div class="gallery"><a class="cover" href="/g/42/"><div class="caption">Example gallery</div></a></div>
		""";

	/// <summary>Checks gallery/search requests, lost cookies, bounded recovery, proxy selection, and cancellation.</summary>
	/// <param name="token">The cancellation token for the regression checks.</param>
	/// <returns>A task that completes when all age-gate checks pass.</returns>
	public static async Task Run(CancellationToken token)
	{
		var useProxy = NhentaiNetSource.UseProxy;
		NhentaiNetSource.UseProxy = false;
		try
		{
			await CheckLoaderPaths(token);
			await CheckRetryCookieLoss(token);
			await CheckRejectedConfirmation(token);
			await CheckCancellation(token);
			NhentaiNetSource.UseProxy = true;
			await CheckProxiedRecovery(token);
		}
		finally
		{
			NhentaiNetSource.UseProxy = useProxy;
		}
	}

	private static async Task CheckLoaderPaths(CancellationToken token)
	{
		var rig = Create((request, _) => Reply(request.Cookies.Any(cookie => cookie.Name == "age_verified")
			? GALLERY : AGE_GATE));
		var manga = await rig.Source.Manga("42", token);
		rig.Api.ClearCookies();
		var pages = await rig.Source.ChapterPages("42", "42", token);
		var results = await rig.Source.Search("example", 1, token);
		Require(manga?.Title == "Example gallery" && pages.Length == 1 && results.Length == 1,
			"Age confirmation must apply to gallery metadata, chapter pages, and search results.");
		Require(rig.Requests.Count == 3 && rig.Requests.All(request => request.Proxy is null),
			"Direct loader requests must work without any configured proxy URLs.");
		foreach (var request in rig.Requests)
		{
			var cookie = request.Cookies.Single(cookie => cookie.Name == "age_verified");
			Require(cookie.Value == "1" && cookie.Domain == "nhentai.net" && cookie.Path == "/" && cookie.Secure &&
				cookie.SameSite == "Lax" && cookie.Expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				"The age cookie must match the site's birthday form and remain scoped to nhentai.net.");
		}
	}

	private static async Task CheckRetryCookieLoss(CancellationToken token)
	{
		var rig = Create((_, call) => call switch
		{
			1 => throw new HttpRequestException("Temporary connection failure"),
			2 => Reply(AGE_GATE),
			_ => Reply(GALLERY)
		});
		await rig.Source.GetHtml(URL, token);
		Require(rig.Requests.Count == 3 && rig.Requests[0].Cookies.Length == 1 &&
			rig.Requests[1].Cookies.Length == 0 && rig.Requests[2].Cookies.Length == 1,
			"A solver retry that clears cookies must reapply age confirmation before returning gallery HTML.");
	}

	private static async Task CheckRejectedConfirmation(CancellationToken token)
	{
		var rig = Create((_, _) => Reply(AGE_GATE));
		try
		{
			await rig.Source.GetHtml(URL, token);
			throw new InvalidOperationException("A rejected age confirmation was returned as gallery HTML.");
		}
		catch (HttpRequestException ex)
		{
			Require(rig.Requests.Count == 2 && ex.Message.Contains("age confirmation"),
				"Age-gate retries must stop after two responses and report the rejected confirmation.");
		}
	}

	private static async Task CheckProxiedRecovery(CancellationToken token)
	{
		var rig = Create((_, call) => Reply(call == 1 ? AGE_GATE : GALLERY));
		await rig.Source.GetHtml(URL, token);
		Require(rig.Requests.Count == 2 && rig.Requests.All(request =>
			request.Proxy?.Url == "socks5://127.0.0.1:1080"),
			"Age-gate recovery must keep the proxy selected for the original request.");
	}

	private static async Task CheckCancellation(CancellationToken token)
	{
		var pending = new TaskCompletionSource<SolverResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var rig = Create((_, _) =>
		{
			started.TrySetResult();
			return pending.Task;
		});
		var first = rig.Source.GetHtml(URL, token);
		await started.Task.WaitAsync(token);
		using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
		cancelled.Cancel();
		try
		{
			await rig.Source.GetHtml(URL, cancelled.Token);
			throw new InvalidOperationException("Cancellation while waiting for the cookie lock was ignored.");
		}
		catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
		{
			Require(rig.Requests.Count == 1, "A cancelled queued request must not contact the solver.");
		}
		finally
		{
			pending.TrySetResult(Solution(GALLERY));
			await first;
		}
	}

	private static Rig Create(Func<Request, int, Task<SolverResponse?>> response)
	{
		var requests = new List<Request>();
		var backend = Stub<IFlareSolverBase>((method, args) =>
		{
			if (method.Name != "Get")
				throw new InvalidOperationException(method.Name);
			var cookies = args![1] as SolverCookie[] ?? [];
			var snapshot = JsonSerializer.Deserialize<SolverCookie[]>(JsonSerializer.Serialize(cookies)) ?? [];
			var request = new Request(snapshot, args[2] as SolverProxy);
			requests.Add(request);
			return response(request, requests.Count);
		});
		var api = new FlareSolverInstance(backend, NullLogger.Instance)
		{
			MaxRetries = 1,
			PauseDurationSecondsMin = 0,
			PauseDurationSecondsMax = 1
		};
		var flare = Stub<IFlareSolverService>((method, _) => method.Name == "Limiter"
			? api : throw new InvalidOperationException(method.Name));
		var proxy = Stub<IProxiedHttpService>((method, _) => method.Name == "GetConfig" && NhentaiNetSource.UseProxy
			? (new[] { "socks5://127.0.0.1:1080" }, 10, 10d) : throw new InvalidOperationException(method.Name));
		var source = new NhentaiNetSource(Stub<IDbService>(), flare, new ConfigurationBuilder().Build(),
			Stub<IHttpService>(), proxy, NullLogger<NhentaiNetSource>.Instance);
		return new(source, api, requests);
	}

	private static SolverResponse Solution(string html) => new()
	{
		Status = "ok", Solution = new() { Status = 200, Url = URL, Response = html }
	};

	private static Task<SolverResponse?> Reply(string html) => Task.FromResult<SolverResponse?>(Solution(html));

	private static T Stub<T>(Func<MethodInfo, object?[]?, object?>? handler = null) where T : class
	{
		var proxy = DispatchProxy.Create<T, Calls>();
		((Calls)(object)proxy).Handler = handler;
		return proxy;
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

	private sealed record class Request(SolverCookie[] Cookies, SolverProxy? Proxy);
	private sealed record class Rig(NhentaiNetSource Source, FlareSolverInstance Api, List<Request> Requests);

	/// <summary>Rejects calls outside the explicitly controlled in-memory dependencies.</summary>
	public class Calls : DispatchProxy
	{
		/// <summary>The allowed behavior, or null when all calls are forbidden.</summary>
		public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

		/// <inheritdoc />
		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
			targetMethod is not null && Handler is not null ? Handler(targetMethod, args)
				: throw new InvalidOperationException($"Unexpected dependency access: {targetMethod?.Name}");
	}
}
