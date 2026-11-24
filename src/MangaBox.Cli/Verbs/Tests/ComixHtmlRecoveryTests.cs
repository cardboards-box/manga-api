using CardboardBox.Json;
using MangaBox.Services.Imaging;
using MangaBox.Utilities.Comix;
using MangaBox.Utilities.Flare;
using MangaBox.Utilities.Flare.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Exercises browser-session failover through the real solver HTTP adapter with in-memory responses.</summary>
internal static class ComixHtmlRecoveryTests
{
	private const string CHAPTER_URL = "https://comix.to/title/example/1-chapter-1";
	private const string CONNECTION_RESET = "Error solving the challenge. Message: unknown error: net::ERR_CONNECTION_RESET";

	/// <summary>Checks diagnostics, retry limits, session replacement, concurrency, cleanup, and cancellation.</summary>
	/// <param name="token">The cancellation token for the harness.</param>
	/// <returns>A task that completes after all regression checks pass.</returns>
	public static async Task Run(CancellationToken token)
	{
		var debug = ComixHtmlService.DEBUG;
		ComixHtmlService.DEBUG = false;
		try
		{
			await CheckDiagnostics(token);
			await CheckInstanceRetries(token);
			foreach (var message in new[] { CONNECTION_RESET, "Session not found after solver restart" })
				await CheckRecovery(message, token);
			await CheckRestartAndReuse(token);
			await CheckExhaustion(token);
			await CheckCancellation(token);
		}
		finally
		{
			ComixHtmlService.DEBUG = debug;
		}
	}

	private static async Task CheckDiagnostics(CancellationToken token)
	{
		var rig = Create((_, _) => Failure(CONNECTION_RESET + "\\nStacktrace: internal browser details"));
		var error = await ExpectFailure(() => rig.Flare.Get(CHAPTER_URL, token: token));
		Require(error.StatusCode == HttpStatusCode.InternalServerError && error.Message.Contains("ERR_CONNECTION_RESET") &&
			!error.Message.Contains("Stacktrace"), "Solver HTTP errors must preserve a bounded browser diagnostic.");

		rig = Create((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
		{
			Content = new StringContent("<html>gateway error</html>")
		});
		error = await ExpectFailure(() => rig.Flare.Get(CHAPTER_URL, token: token));
		Require(error.StatusCode == HttpStatusCode.BadGateway && !error.Message.Contains("<html>"),
			"Non-JSON solver failures must retain the HTTP code without logging response bodies.");

		rig = Create((_, _) => Json(new { status = "error", message = "Session unavailable" }));
		error = await ExpectFailure(() => rig.Flare.Get(CHAPTER_URL, token: token));
		Require(error.Message.Contains("Session unavailable"), "HTTP 200 solver error envelopes must fail explicitly.");
	}

	private static async Task CheckInstanceRetries(CancellationToken token)
	{
		foreach (var retries in new[] { 0, 2 })
		{
			var rig = Create((_, _) => Failure(CONNECTION_RESET));
			var instance = new FlareSolverInstance(rig.Flare, NullLogger.Instance)
			{
				MaxRetries = retries,
				PauseDurationSecondsMin = 0,
				PauseDurationSecondsMax = 1
			};
			await ExpectFailure(() => instance.GetHtml(CHAPTER_URL, token));
			Require(rig.Transport.Requests.Count == retries + 1,
				"MaxRetries must mean additional retries, including exactly one request when set to zero.");
		}
	}

	private static async Task CheckRecovery(string message, CancellationToken token)
	{
		var rig = Create((_, call) => call == 1 ? Failure(message) : Success());
		rig.Transport.FailCleanup = true;
		await using (rig.Html)
		{
			var page = await rig.Html.GetHtml(CHAPTER_URL, token);
			Require(page.DocumentNode.InnerText == "Reader ready" && rig.Transport.Requests.Count == 2 &&
				rig.Transport.CreatedProxies.SequenceEqual(["socks5://127.0.0.1:1080", "socks5://127.0.0.1:1081"]),
				"A solver failure must replace its session on another proxy before retrying.");
			Require(rig.Transport.Requests[0].SessionId != rig.Transport.Requests[1].SessionId &&
				rig.Transport.Destroyed.Count == 1, "The failed session must be discarded even when cleanup fails.");
		}
		Require(rig.Transport.Destroyed.Count == 2, "Disposing the source must also remove the replacement session.");
	}

	private static async Task CheckRestartAndReuse(CancellationToken token)
	{
		var rig = Create((_, call) => call == 2 ? Failure("Session not found") : Success());
		await using var html = rig.Html;
		await html.GetHtml(CHAPTER_URL, token);
		await Task.WhenAll(html.GetHtml(CHAPTER_URL, token), html.GetHtml(CHAPTER_URL, token));
		Require(rig.Transport.Requests.Count == 4 && rig.Transport.CreatedProxies.Count == 2,
			"Concurrent requests must share the replacement browser session after a solver restart.");
		Require(rig.Transport.Requests[1].Cookies?.Length == 1 && rig.Transport.Requests[2].Cookies is null &&
			rig.Transport.Requests[2].SessionId == rig.Transport.Requests[3].SessionId,
			"Old proxy-bound cookies must not be replayed into the replacement session.");
	}

	private static async Task CheckExhaustion(CancellationToken token)
	{
		var rig = Create((_, call) => call <= 4 ? Failure(CONNECTION_RESET) : Success());
		await using var html = rig.Html;
		var error = await ExpectFailure(() => html.GetHtml(CHAPTER_URL, token));
		Require(rig.Transport.Requests.Count == 4 && rig.Transport.CreatedProxies.Count == 4 &&
			rig.Transport.Destroyed.Count == 4 && error.Message.Contains("ERR_CONNECTION_RESET"),
			"HTML recovery must stop after four attempts, preserving the cause and cleaning up every failed session.");
		await html.GetHtml(CHAPTER_URL, token);
		Require(rig.Transport.CreatedProxies.Count == 5, "The next request must not reuse the last failed session.");
	}

	private static async Task CheckCancellation(CancellationToken token)
	{
		var rig = Create((_, _) => Failure(CONNECTION_RESET));
		await using var html = rig.Html;
		using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
		cancelled.CancelAfter(TimeSpan.FromMilliseconds(200));
		try
		{
			await html.GetHtml(CHAPTER_URL, cancelled.Token);
			throw new InvalidOperationException("HTML retry cancellation was ignored.");
		}
		catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
		{
			Require(rig.Transport.Requests.Count == 1 && rig.Transport.Destroyed.Count == 1,
				"Cancellation during backoff must stop without creating another session.");
		}
	}

	private static Rig Create(Func<SolverRequest, int, HttpResponseMessage> response)
	{
		var transport = new Transport(response);
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["FlareSolver:Url"] = "https://solver.test",
			["Proxies:Urls:0"] = "socks5://127.0.0.1:1080",
			["Proxies:Urls:1"] = "socks5://127.0.0.1:1081",
			["Proxies:Tokens"] = "1000"
		}).Build();
		var json = new SystemTextJsonService(new JsonSerializerOptions(JsonSerializerDefaults.Web));
		var api = new ApiService(transport, json);
		var solver = new FlareSolverService(new FlareSolverApiService(api, config), NullLogger<FlareSolverService>.Instance);
		var proxies = new ProxiedHttpService(Forbidden<IHttpService>(), config, NullLogger<ProxiedHttpService>.Instance);
		var html = new ComixHtmlService(api, Forbidden<IComixWAFService>(), solver, proxies,
			NullLogger<ComixHtmlService>.Instance);
		return new(transport, solver, html);
	}

	private static HttpResponseMessage Success() => Json(new SolverResponse
	{
		Status = "ok",
		Solution = new()
		{
			Status = 200,
			Url = CHAPTER_URL,
			Response = "<html><title>Reader ready</title></html>",
			Cookies = [new("cf_clearance", "old-proxy-cookie")]
		}
	});

	private static HttpResponseMessage Failure(string message) =>
		Json(new { status = "error", message }, HttpStatusCode.InternalServerError);

	private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
	{
		Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
	};

	private static async Task<HttpRequestException> ExpectFailure(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (HttpRequestException ex)
		{
			return ex;
		}
		throw new InvalidOperationException("Expected a solver HTTP failure.");
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

	private static T Forbidden<T>() where T : class => DispatchProxy.Create<T, RejectCalls>();

	private sealed record class Rig(Transport Transport, IFlareSolverService Flare, ComixHtmlService Html);

	/// <summary>Rejects dependencies that the browser recovery path must not invoke.</summary>
	public class RejectCalls : DispatchProxy
	{
		/// <inheritdoc />
		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
			throw new InvalidOperationException($"Unexpected dependency access: {targetMethod?.Name}");
	}

	private sealed class Transport(Func<SolverRequest, int, HttpResponseMessage> _response) : IHttpClientFactory
	{
		public List<SolverRequest> Requests { get; } = [];
		public List<string> CreatedProxies { get; } = [];
		public List<string> Destroyed { get; } = [];
		public bool FailCleanup { get; set; }
		public HttpClient CreateClient(string name) => new(new Handler(this));

		public HttpResponseMessage Respond(SolverRequest request)
		{
			if (request.Command == SolverRequest.CMD_SESSION_CREATE)
			{
				CreatedProxies.Add(request.Proxy?.Url ?? "");
				return Json(new SolverSessionCreate { Status = "ok", SessionId = $"session-{CreatedProxies.Count}" });
			}
			if (request.Command == SolverRequest.CMD_SESSION_DESTROY)
			{
				Destroyed.Add(request.SessionId ?? "");
				return FailCleanup ? Failure("Session already removed") : Json(new SolverSessionDestroy { Status = "ok" });
			}
			Requests.Add(request);
			return _response(request, Requests.Count);
		}

	}

	private sealed class Handler(Transport _owner) : HttpMessageHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
		{
			var body = await request.Content!.ReadAsStringAsync(token);
			var command = JsonSerializer.Deserialize<SolverRequest>(body)
				?? throw new InvalidDataException("Missing solver request.");
			return _owner.Respond(command);
		}
	}
}
