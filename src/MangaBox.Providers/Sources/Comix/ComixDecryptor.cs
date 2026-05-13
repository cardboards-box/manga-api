using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.ClearScript.V8;

namespace MangaBox.Providers.Sources.Comix;

public static class ComixDecryptor
{
	private const string SecureBundleResourceName = "MangaBox.Providers.Sources.Comix.impl.secure-teup0d-D6PE046x.js";
	private const string NodeDecryptScriptResourceName = "MangaBox.Providers.Sources.Comix.impl.comix-node-decrypt.js";
	private static readonly UTF8Encoding StrictUtf8 = new(false, true);
	private static readonly Lock DecryptLock = new();
	private static V8ScriptEngine? _decryptEngine;
	private static bool _decryptEngineInitialized;
	private static string _decryptInitError = string.Empty;
	private static string _nodeDecryptScriptPath = string.Empty;

	public static bool TryDecryptPayload(string encryptedPayload, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(encryptedPayload))
			return false;

		lock (DecryptLock)
		{
			var started = Stopwatch.StartNew();
			const int maxTotalMs = 65000;
			const int maxVmMsFirstLayer = 9000;
			const int maxVmMsDeeperLayers = 30000;
			var current = encryptedPayload;
			var seen = new HashSet<string>(StringComparer.Ordinal);
			var trace = new List<string>(capacity: 16);
			const int maxDepth = 8;
			for (var depth = 0; depth < maxDepth; depth++)
			{
				if (started.ElapsedMilliseconds > maxTotalMs)
				{
					trace.Add($"d{depth}:timeout={started.ElapsedMilliseconds}");
					break;
				}

				if (string.IsNullOrWhiteSpace(current) || !seen.Add(current))
				{
					trace.Add($"d{depth}:stop");
					break;
				}

				if (current.StartsWith("__", StringComparison.Ordinal))
				{
					trace.Add($"d{depth}:marker={current}");
					break;
				}

				if (TryDecodeBase64UrlLayer(current, out json))
					return true;

				var nextPayload = string.Empty;
				if (TryDecodeBase64UrlNextLayer(current, seen, out var decodedNextLayer))
					nextPayload = decodedNextLayer;

				var vmBudget = depth == 0 ? maxVmMsFirstLayer : maxVmMsDeeperLayers;
				if (TryDecryptPayloadWithLiveVm(current, seen, vmBudget, out json, out var vmNextPayload))
					return true;
				if (string.IsNullOrWhiteSpace(nextPayload))
					nextPayload = vmNextPayload;

				if (!string.IsNullOrWhiteSpace(nextPayload) && seen.Contains(nextPayload))
					nextPayload = string.Empty;

				if (started.ElapsedMilliseconds > maxTotalMs)
				{
					trace.Add($"d{depth}:timeout={started.ElapsedMilliseconds}");
					break;
				}

				if (started.ElapsedMilliseconds <= maxTotalMs && TryDecryptPayloadLegacy(current, out json))
					return true;

				if (depth == 0 && !string.IsNullOrWhiteSpace(nextPayload) && nextPayload.Length > current.Length * 2)
					nextPayload = string.Empty;

				var nodeNextPayload = string.Empty;
				var nodeInfo = string.Empty;
				var shouldTryNode = depth > 0 && current.Length <= 12000;
				if (shouldTryNode && TryDecryptPayloadWithNode(current, seen, out json, out nodeNextPayload, out nodeInfo))
					return true;

				if (shouldTryNode && !string.IsNullOrWhiteSpace(nodeNextPayload))
				{
					var nodeMarker = nodeNextPayload.StartsWith("__", StringComparison.Ordinal);
					if (!nodeMarker)
					{
						nextPayload = nodeNextPayload;
					}
					else if (nodeNextPayload.Length <= current.Length)
					{
						nextPayload = nodeNextPayload;
					}
				}
				else if (!shouldTryNode && string.IsNullOrWhiteSpace(nextPayload))
				{
					nodeInfo = "skipped-large";
				}

				var nextHead = string.IsNullOrWhiteSpace(nextPayload)
					? string.Empty
					: nextPayload[..Math.Min(36, nextPayload.Length)];
				var nodeTail = string.IsNullOrWhiteSpace(nodeInfo) ? string.Empty : $",node={nodeInfo}";
				trace.Add($"d{depth}:in={current.Length},next={nextPayload?.Length ?? 0},head={nextHead}{nodeTail}");

				if (string.IsNullOrWhiteSpace(nextPayload) || nextPayload.StartsWith("__", StringComparison.Ordinal))
					break;

				current = nextPayload;
			}

			if (trace.Count > 0 && trace.Count >= maxDepth)
				trace.Add("max-depth");
			trace.Add($"elapsedMs={started.ElapsedMilliseconds}");

			json = trace.Count == 0
				? string.Empty
				: "__comixTrace__ " + string.Join(" | ", trace);
			return false;
		}
	}

	public static void WarmupDecryptEngine()
	{
		lock (DecryptLock)
		{
			_ = EnsureDecryptEngineInitialized();
		}
	}

	private static bool TryDecodeBase64UrlLayer(string value, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(value) || value.StartsWith("__", StringComparison.Ordinal))
			return false;

		try
		{
			var bytes = ComixToSigner.DecodeBase64Url(value.Trim());
			if (bytes.Length == 0)
				return false;

			if (TryNormalizeDecodedBytes(bytes, out json))
				return true;

			if (TryDecompressGzip(bytes, out var inflated) && TryNormalizeDecodedBytes(inflated, out json))
				return true;

			return false;
		}
		catch
		{
			json = string.Empty;
			return false;
		}
	}

	private static bool TryDecodeBase64UrlNextLayer(string value, HashSet<string> seenLayers, out string nextLayer)
	{
		nextLayer = string.Empty;
		if (string.IsNullOrWhiteSpace(value) || value.StartsWith("__", StringComparison.Ordinal))
			return false;

		try
		{
			var bytes = ComixToSigner.DecodeBase64Url(value.Trim());
			if (bytes.Length == 0)
				return false;

			string text;
			try
			{
				text = StrictUtf8.GetString(bytes);
			}
			catch
			{
				text = Encoding.Latin1.GetString(bytes);
			}

			var candidate = ExtractBase64UrlCandidate(text, value);
			if (string.IsNullOrWhiteSpace(candidate) || seenLayers.Contains(candidate))
				return false;

			nextLayer = candidate;
			return true;
		}
		catch
		{
			nextLayer = string.Empty;
			return false;
		}
	}

	private static bool TryDecryptPayloadWithLiveVm(string encryptedPayload, HashSet<string> seenLayers, int maxVmMs, out string json, out string nextPayload)
	{
		json = string.Empty;
		nextPayload = string.Empty;

		try
		{
			if (!EnsureDecryptEngineInitialized())
			{
				var initReason = string.IsNullOrWhiteSpace(_decryptInitError) ? "unknown" : _decryptInitError;
				nextPayload = $"__vm_init_failed__:{initReason}";
				return false;
			}

			if (_decryptEngine is null)
			{
				nextPayload = "__vm_engine_null__";
				return false;
			}

			_decryptEngine.Script.encryptedPayload = encryptedPayload;
			_decryptEngine.Script.seenLayers = seenLayers?.ToArray() ?? Array.Empty<string>();
			_decryptEngine.Script.vmBudgetMs = maxVmMs;
			var value = _decryptEngine.Evaluate(@"
				(function() {
					try {
						if (typeof Ii === 'undefined' || !Ii) return '__vm_no_ii__';
						var vmStarted = Date.now();
						function outOfBudget() { return (Date.now() - vmStarted) > (typeof vmBudgetMs === 'number' ? vmBudgetMs : 9000); }


					function bytesToText(bytes) {
						var out = '';
						for (var i = 0; i < bytes.length; i++) out += String.fromCharCode(bytes[i] & 255);
						return out;
					}

					function asText(v) {
						if (v == null) return null;
						if (typeof v === 'string') return v;
						try {
							if (typeof ArrayBuffer !== 'undefined') {
								if (typeof ArrayBuffer.isView === 'function' && ArrayBuffer.isView(v))
									return bytesToText(v);
								if (v instanceof ArrayBuffer)
									return bytesToText(new Uint8Array(v));
							}
						} catch (e) {}

						try {
							if (typeof v.length === 'number' && v.length >= 0) {
								var out = '';
								for (var i = 0; i < v.length; i++) {
									var n = v[i];
									if (typeof n === 'number') out += String.fromCharCode(n & 255);
									else if (typeof n === 'string' && n.length > 0) out += n.charAt(0);
									else return null;
								}
								return out;
							}
						} catch (e) {}

						try {
							if (typeof v === 'object') return JSON.stringify(v);
						} catch (e) {}

						try { return String(v); } catch (e) { return null; }
					}

					function normalizeText(text) {
						if (typeof text !== 'string') return null;
						var current = text.trim();

						for (var i = 0; i < 4; i++) {
							if (!current || current.indexOf('%') < 0) break;
							try {
								var decoded = decodeURIComponent(current);
								if (decoded === current) break;
								current = decoded;
							} catch (e) {
								break;
							}
						}

						if (current.length > 1 && current.charCodeAt(0) === 34 && current.charCodeAt(current.length - 1) === 34) {
							try {
								var unwrapped = JSON.parse(current);
								if (typeof unwrapped === 'string') current = unwrapped;
							} catch (e) {}
						}

						return current;
					}

					function runPendingTasks(maxRounds) {
						if (!globalThis.__vmTaskQueue || !globalThis.__vmTaskQueue.length) return;
						var rounds = typeof maxRounds === 'number' ? maxRounds : 3;
						for (var r = 0; r < rounds; r++) {
							if (!globalThis.__vmTaskQueue.length) break;
							var tasks = globalThis.__vmTaskQueue.slice(0);
							globalThis.__vmTaskQueue.length = 0;
							for (var t = 0; t < tasks.length; t++) {
								try { tasks[t](); } catch (e) {}
							}
						}
					}

					function looksLikeJson(text) {
						if (typeof text !== 'string') return false;
						var t = text.trim();
						if (!t) return false;
						if (t.charCodeAt(0) === 123 || t.charCodeAt(0) === 91) return true;
						if (t.indexOf('%7B') === 0 || t.indexOf('%5B') === 0) return true;
						if (t.indexOf('%22status%22') >= 0) return true;
						return false;
					}

					function looksLikeBase64Layer(text) {
						if (typeof text !== 'string') return false;
						if (text.length < 80) return false;
						for (var bi = 0; bi < text.length; bi++) {
							var ch = text.charCodeAt(bi);
							var isUpper = ch >= 65 && ch <= 90;
							var isLower = ch >= 97 && ch <= 122;
							var isNum = ch >= 48 && ch <= 57;
							if (!(isUpper || isLower || isNum || ch == 95 || ch == 45 || ch == 43 || ch == 47 || ch == 61)) return false;
						}
						return true;
					}

					function keyOf(v) {
						var text = asText(v);
						if (!text) return null;
						return text.length + ':' + text.slice(0, 120);
					}

					var namedOps = {
						D: typeof Ii.D == 'function' ? Ii.D : null,
						R: typeof Ii.R == 'function' ? Ii.R : null,
						I: typeof Ii.I == 'function' ? Ii.I : null
					};
					var opOrder = ['D', 'R', 'I'];
					var availableOps = [];
					for (var on = 0; on < opOrder.length; on++) {
						if (typeof namedOps[opOrder[on]] == 'function') availableOps.push(opOrder[on]);
					}
					if (availableOps.length == 0) return '__vm_no_ops__';

					function applyRawOp(name, input) {
						var fn = namedOps[name];
						if (!fn) return null;
						try { return fn(input); } catch (e) { return null; }
					}

					function asNormalizedText(value) {
						if (value == null) return null;
						var text = normalizeText(asText(value));
						return typeof text == 'string' && text.length > 0 ? text : null;
					}

					function scoreCandidate(text, inputLen) {
						if (typeof text !== 'string' || !text.length) return -2147483648;
						var score = 0;
						if (text.indexOf('%22status%22') >= 0) score += 1200;
						if (text.indexOf('%7B') >= 0 || text.indexOf('%5B') >= 0) score += 700;
						if (text.indexOf('%7B') === 0 || text.indexOf('%5B') === 0) score += 400;
						if (text.length <= inputLen) score += 300;
						score -= Math.floor(Math.min(text.length, 200000) / 256);
						return score;
					}

					function tryDecodeBase64ToText(layer) {
						if (!looksLikeBase64Layer(layer)) return null;
						try {
							var decoded = atob(String(layer).replace(/-/g, '+').replace(/_/g, '/'));
							return normalizeText(decoded);
						} catch (e) {
							return null;
						}
					}

					runPendingTasks(6);
					var bestBase64 = null;
					var bestBase64Score = -2147483648;
					var fallback = null;
					var seenMap = {};
					seenMap[String(encryptedPayload)] = true;
					try {
						if (seenLayers && typeof seenLayers.length === 'number') {
							for (var svi = 0; svi < seenLayers.length; svi++) {
								var sv = seenLayers[svi];
								if (sv == null) continue;
								var svText = String(sv).trim();
								if (svText.length > 0) seenMap[svText] = true;
							}
						}
					} catch (e) {}

					function considerTextCandidate(text, inputLen) {
						if (!text) return;
						var key = String(text).trim();
						if (!key) return;
						if (seenMap[key]) return;
						if (looksLikeJson(key)) return key;
						if (looksLikeBase64Layer(key)) {
							var candidateScore = scoreCandidate(key, inputLen || encryptedPayload.length || key.length);
							if (candidateScore > bestBase64Score || (candidateScore === bestBase64Score && (!bestBase64 || key.length < bestBase64.length))) {
								bestBase64 = key;
								bestBase64Score = candidateScore;
							}
							var decoded = tryDecodeBase64ToText(key);
							if (decoded) {
								var decodedKey = String(decoded).trim();
								if (decodedKey && !seenMap[decodedKey] && looksLikeJson(decodedKey)) return decodedKey;
							}
						} else if (!fallback) {
							fallback = key;
						}
						return null;
					}

					var deterministicPaths = [];
					var maxPathDepth = 5;
					var maxPaths = 120;
					for (var depth = 1; depth <= maxPathDepth; depth++) {
						var build = function(prefix, remaining, lastOp) {
							if (outOfBudget()) return;
							if (deterministicPaths.length >= maxPaths) return;
							if (remaining === 0) {
								deterministicPaths.push(prefix.slice(0));
								return;
							}
							for (var oi = 0; oi < availableOps.length; oi++) {
								if (outOfBudget()) return;
								if (deterministicPaths.length >= maxPaths) return;
								var opName = availableOps[oi];
								if (lastOp && opName === lastOp) continue;
								prefix.push(opName);
								build(prefix, remaining - 1, opName);
								prefix.pop();
							}
						};
						build([], depth, null);
						if (outOfBudget() || deterministicPaths.length >= maxPaths) break;
					}

					for (var pi = 0; pi < deterministicPaths.length; pi++) {
						if (outOfBudget()) break;
						var path = deterministicPaths[pi];
						var validPath = true;
						for (var ai = 0; ai < path.length; ai++) {
							if (availableOps.indexOf(path[ai]) < 0) { validPath = false; break; }
						}
						if (!validPath) continue;

						var rawCurrent = encryptedPayload;
						for (var si = 0; si < path.length; si++) {
							if (outOfBudget()) break;
							rawCurrent = applyRawOp(path[si], rawCurrent);
							runPendingTasks(2);
							if (rawCurrent == null) break;

							var textCurrent = asNormalizedText(rawCurrent);
							if (!textCurrent) continue;
							var candidateHit = considerTextCandidate(textCurrent, encryptedPayload.length || textCurrent.length);
							if (candidateHit) return candidateHit;

							if (looksLikeBase64Layer(textCurrent)) {
								try {
									var decodedForNext = atob(String(textCurrent).replace(/-/g, '+').replace(/_/g, '/'));
									rawCurrent = decodedForNext;
									runPendingTasks(1);
									var decodedHit = considerTextCandidate(normalizeText(decodedForNext), encryptedPayload.length || textCurrent.length);
									if (decodedHit) return decodedHit;
								} catch (e) {
								}
							}
						}
					}

					if (bestBase64 && bestBase64 !== encryptedPayload) return bestBase64;
					if (fallback && fallback !== encryptedPayload) return fallback;
					return '__vm_no_candidate__';
				} catch (err) {
					try {
						return JSON.stringify({ __vmError: String(err && err.message ? err.message : err), __vmStack: String(err && err.stack ? err.stack : '') });
					} catch (x) {
						return '__vm_error__';
					}
				}
				})()
			");
			var raw = value switch
			{
				null => string.Empty,
				string s => s,
				_ => value.ToString() ?? string.Empty,
			};

			if (!string.IsNullOrWhiteSpace(raw) && raw.Contains("__vmError", StringComparison.Ordinal))
			{
				try
				{
					using var errDoc = JsonDocument.Parse(raw);
					if (errDoc.RootElement.TryGetProperty("__vmError", out var vmErr))
					{
						var errMessage = vmErr.GetString() ?? string.Empty;
						var stack = errDoc.RootElement.TryGetProperty("__vmStack", out var vmStack)
							? vmStack.GetString() ?? string.Empty
							: string.Empty;
						throw new InvalidOperationException($"Comix VM decryption error: {errMessage}\n{stack}");
					}
				}
				catch (JsonException)
				{
				}
			}

			if (TryNormalizeVmDecryption(raw, out json))
				return true;

			nextPayload = ExtractBase64UrlCandidate(raw, encryptedPayload);
			if (string.IsNullOrWhiteSpace(nextPayload) && !string.IsNullOrWhiteSpace(raw) && raw.StartsWith("__vm_", StringComparison.Ordinal))
				nextPayload = raw.Trim();
			return false;
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch
		{
			json = string.Empty;
			nextPayload = string.Empty;
			return false;
		}
	}

	private static bool TryDecryptPayloadWithNode(string encryptedPayload, HashSet<string> seenLayers, out string json, out string nextPayload, out string nodeInfo)
	{
		json = string.Empty;
		nextPayload = string.Empty;
		nodeInfo = string.Empty;

		try
		{
			if (!TryEnsureNodeDecryptScriptPath(out var scriptPath))
			{
				nextPayload = "__node_script_missing__";
				return false;
			}

			var startInfo = new ProcessStartInfo
			{
				FileName = "node",
				Arguments = $"\"{scriptPath}\"",
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? AppContext.BaseDirectory,
			};

			using var process = Process.Start(startInfo);
			if (process is null)
			{
				nextPayload = "__node_start_failed__";
				return false;
			}

			process.StandardInput.Write(encryptedPayload);
			process.StandardInput.Close();

			var stdoutTask = process.StandardOutput.ReadToEndAsync();
			var stderrTask = process.StandardError.ReadToEndAsync();
			var exitTask = process.WaitForExitAsync();
			var allDoneTask = Task.WhenAll(stdoutTask, stderrTask, exitTask);

			var timeoutMs = Math.Clamp(10000 + encryptedPayload.Length, 12000, 28000);
			var completed = Task.WhenAny(allDoneTask, Task.Delay(timeoutMs)).GetAwaiter().GetResult();
			if (completed != allDoneTask)
			{
				try { process.Kill(true); } catch { }
				var err = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
				nextPayload = string.IsNullOrWhiteSpace(err) ? $"__node_timeout__:{timeoutMs}" : "__node_timeout__:" + err.Trim();
				return false;
			}

			var stdout = stdoutTask.GetAwaiter().GetResult();
			var stderr = stderrTask.GetAwaiter().GetResult();

			if (string.IsNullOrWhiteSpace(stdout))
			{
				nextPayload = string.IsNullOrWhiteSpace(stderr) ? "__node_no_output__" : "__node_err__:" + stderr.Trim();
				return false;
			}

			using var doc = JsonDocument.Parse(stdout);
			var root = doc.RootElement;

			if (root.TryGetProperty("path", out var pathElement))
				nodeInfo = pathElement.GetString() ?? string.Empty;

			if (root.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True)
			{
				if (root.TryGetProperty("json", out var jsonElement))
				{
					var candidateJson = jsonElement.GetString() ?? string.Empty;
					if (TryNormalizeVmDecryption(candidateJson, out json))
						return true;
				}
			}

			if (root.TryGetProperty("candidates", out var candidatesElement) && candidatesElement.ValueKind == JsonValueKind.Array)
			{
				var candidates = new List<string>();
				foreach (var item in candidatesElement.EnumerateArray())
				{
					var candidate = item.GetString() ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(candidate))
						candidates.Add(candidate);
				}

				foreach (var candidate in candidates)
				{
					if (TryNormalizeVmDecryption(candidate, out json))
						return true;
					if (TryDecodeBase64UrlLayer(candidate, out json))
						return true;
					if (TryDecryptPayloadLegacy(candidate, out json))
						return true;
				}

				var filtered = candidates
					.Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, encryptedPayload, StringComparison.Ordinal))
					.ToList();

				var preferred = filtered
					.OrderByDescending(ScoreDecryptCandidate)
					.ThenBy(c => c.Length)
					.FirstOrDefault(c => !seenLayers.Contains(c))
					?? filtered.OrderByDescending(ScoreDecryptCandidate).ThenBy(c => c.Length).FirstOrDefault();

				nextPayload = preferred ?? string.Empty;
				return false;
			}

			if (root.TryGetProperty("next", out var nextElement))
			{
				nextPayload = nextElement.GetString() ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(nextPayload) && seenLayers.Contains(nextPayload))
					nextPayload = string.Empty;
				if (TryNormalizeVmDecryption(nextPayload, out json))
					return true;
				if (TryDecodeBase64UrlLayer(nextPayload, out json))
					return true;
				if (TryDecryptPayloadLegacy(nextPayload, out json))
					return true;
				return false;
			}

			if (root.TryGetProperty("error", out var errorElement))
			{
				nextPayload = "__node__:" + (errorElement.GetString() ?? "unknown");
				if (!string.IsNullOrWhiteSpace(nodeInfo))
					nodeInfo = $"{nodeInfo}:err";
				return false;
			}

			nextPayload = "__node_unknown_output__";
			return false;
		}
		catch (Exception ex)
		{
			nextPayload = "__node_exception__:" + ex.GetType().Name + ":" + ex.Message;
			nodeInfo = string.IsNullOrWhiteSpace(nodeInfo) ? "exception" : nodeInfo + ":exception";
			json = string.Empty;
			return false;
		}
	}

	private static bool TryEnsureNodeDecryptScriptPath(out string scriptPath)
	{
		scriptPath = string.Empty;

		try
		{
			var asm = typeof(ComixDecryptor).Assembly;
			using var stream = asm.GetManifestResourceStream(NodeDecryptScriptResourceName);
			if (stream is null)
				return false;

			var dir = Path.Combine(AppContext.BaseDirectory, "comix-runtime");
			Directory.CreateDirectory(dir);

			var scriptFile = Path.Combine(dir, "comix-node-decrypt.js");
			using (var file = File.Create(scriptFile))
			{
				stream.CopyTo(file);
			}

			var bundleTarget = Path.Combine(dir, "secure-teup0d-D6PE046x.js");
			using var bundleStream = asm.GetManifestResourceStream(SecureBundleResourceName);
			if (bundleStream is null)
				return false;
			using (var bundleFile = File.Create(bundleTarget))
			{
				bundleStream.CopyTo(bundleFile);
			}

			_nodeDecryptScriptPath = scriptFile;
			scriptPath = scriptFile;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string ExtractBase64UrlCandidate(string raw, string originalPayload)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return string.Empty;

		var seeds = new List<string> { raw, raw.Trim() };
		try
		{
			var unescaped = JsonSerializer.Deserialize<string>(raw);
			if (!string.IsNullOrWhiteSpace(unescaped))
				seeds.Add(unescaped);
		}
		catch
		{
		}

		foreach (var seed in seeds.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
		{
			foreach (var candidate in ExpandDecodingCandidates(seed))
			{
				var trimmed = candidate.Trim();
				if (trimmed.Length <= 80)
					continue;
				if (string.Equals(trimmed, originalPayload, StringComparison.Ordinal))
					continue;
				if (trimmed.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '+' or '/' or '='))
					return trimmed;
			}
		}

		return string.Empty;
	}

	private static bool TryNormalizeVmDecryption(string raw, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(raw))
			return false;

		var candidates = new List<string> { raw, raw.Trim() };
		try
		{
			var unescaped = JsonSerializer.Deserialize<string>(raw);
			if (!string.IsNullOrWhiteSpace(unescaped))
				candidates.Add(unescaped);
		}
		catch
		{
		}

		foreach (var seed in candidates.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
		{
			foreach (var candidate in ExpandDecodingCandidates(seed))
			{
				if (string.IsNullOrWhiteSpace(candidate))
					continue;
				var trimmed = candidate.Trim();
				if (TryValidateJson(trimmed, out json))
					return true;

				var latin1Bytes = Encoding.Latin1.GetBytes(candidate);
				if (TryDecodeJson(latin1Bytes, out json))
					return true;

				if (TryDecompressGzip(latin1Bytes, out var inflated) && TryDecodeJson(inflated, out json))
					return true;
			}
		}

		return false;
	}

	private static IEnumerable<string> ExpandDecodingCandidates(string input)
	{
		yield return input;

		var current = input;
		for (var i = 0; i < 3; i++)
		{
			string decoded;
			try
			{
				decoded = Uri.UnescapeDataString(current);
			}
			catch
			{
				yield break;
			}

			if (string.Equals(decoded, current, StringComparison.Ordinal))
				yield break;

			yield return decoded;
			current = decoded;
		}
	}

	private static int ScoreDecryptCandidate(string candidate)
	{
		if (string.IsNullOrWhiteSpace(candidate))
			return int.MinValue;

		var score = 0;
		if (candidate.Contains("%22status%22", StringComparison.Ordinal)) score += 1200;
		if (candidate.Contains("%7B", StringComparison.Ordinal) || candidate.Contains("%5B", StringComparison.Ordinal)) score += 700;
		if (candidate.StartsWith("%7B", StringComparison.Ordinal) || candidate.StartsWith("%5B", StringComparison.Ordinal)) score += 400;
		if (candidate.Length > 80 && candidate.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '+' or '/' or '=')) score += 150;

		try
		{
			var decoded = ComixToSigner.DecodeBase64Url(candidate);
			if (decoded.Length > 0)
			{
				var decodedText = Encoding.UTF8.GetString(decoded);
				if (decodedText.Contains("\"status\"", StringComparison.Ordinal)) score += 900;
				if (TryValidateJson(decodedText.Trim(), out _)) score += 1600;
			}
		}
		catch
		{
		}

		score -= Math.Min(candidate.Length, 200000) / 256;
		return score;
	}

	private static bool EnsureDecryptEngineInitialized()
	{
		if (_decryptEngineInitialized)
			return _decryptEngine is not null;

		_decryptInitError = string.Empty;

		if (_decryptEngine is not null)
		{
			try
			{
				_decryptEngineInitialized = true;
				return true;
			}
			catch
			{
				_decryptEngine = null;
				_decryptEngineInitialized = false;
			}
		}

		try
		{
			var asm = typeof(ComixDecryptor).Assembly;
			using var stream = asm.GetManifestResourceStream(SecureBundleResourceName);
			if (stream is null)
			{
				_decryptInitError = $"resource-not-found:{SecureBundleResourceName}";
				return false;
			}

			using var reader = new StreamReader(stream);
			var bundle = reader.ReadToEnd();
			var exportIndex = bundle.LastIndexOf("export{", StringComparison.Ordinal);
			if (exportIndex >= 0)
				bundle = bundle[..exportIndex];

			var engine = new V8ScriptEngine();
			engine.Execute(@"
				globalThis.globalThis = globalThis;
				globalThis.window = globalThis;
				globalThis.self = globalThis;
				globalThis.document = globalThis.document || {
					currentScript: null,
					createElement: function() { return {}; },
					addEventListener: function() {},
					removeEventListener: function() {}
				};
				try {
					Object.defineProperty(globalThis, 'navigator', {
						value: { appCodeName: 'Mozilla', userAgent: 'Mozilla/5.0', platform: 'Win32', language: 'en-US' },
						configurable: true,
						writable: true
					});
				} catch (e) {
					globalThis.navigator = globalThis.navigator || {};
					globalThis.navigator.appCodeName = globalThis.navigator.appCodeName || 'Mozilla';
					globalThis.navigator.userAgent = globalThis.navigator.userAgent || 'Mozilla/5.0';
					globalThis.navigator.platform = globalThis.navigator.platform || 'Win32';
					globalThis.navigator.language = globalThis.navigator.language || 'en-US';
				}
				globalThis.location = globalThis.location || { href: 'https://comix.to/', host: 'comix.to', origin: 'https://comix.to' };
				globalThis.performance = globalThis.performance || { now: function(){ return Date.now(); } };
				globalThis.window.addEventListener = globalThis.window.addEventListener || function() {};
				globalThis.window.removeEventListener = globalThis.window.removeEventListener || function() {};
				globalThis.window.dispatchEvent = globalThis.window.dispatchEvent || function() { return true; };
				globalThis.__vmTaskQueue = globalThis.__vmTaskQueue || [];
				globalThis.__vmScheduleTask = function(cb) {
					if (typeof cb === 'function') globalThis.__vmTaskQueue.push(cb);
				};
				if (typeof globalThis.queueMicrotask !== 'function') globalThis.queueMicrotask = function(cb) { globalThis.__vmScheduleTask(cb); };
				if (typeof globalThis.setTimeout !== 'function') globalThis.setTimeout = function(cb) { globalThis.__vmScheduleTask(cb); return globalThis.__vmTaskQueue.length; };
				if (typeof globalThis.clearTimeout !== 'function') globalThis.clearTimeout = function() {};
				if (typeof globalThis.setInterval !== 'function') globalThis.setInterval = function(cb) { globalThis.__vmScheduleTask(cb); return globalThis.__vmTaskQueue.length; };
				if (typeof globalThis.clearInterval !== 'function') globalThis.clearInterval = function() {};
				if (typeof globalThis.requestAnimationFrame !== 'function') globalThis.requestAnimationFrame = function(cb) { globalThis.__vmScheduleTask(cb); return globalThis.__vmTaskQueue.length; };
				if (typeof globalThis.cancelAnimationFrame !== 'function') globalThis.cancelAnimationFrame = function() {};
				if (typeof globalThis.atob !== 'function') {
					globalThis.atob = function(input) {
						var chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';
						var str = String(input).replace(/=+$/, '');
						var output = '';
						for (var bc = 0, bs, buffer, idx = 0; (buffer = str.charAt(idx++)); ~buffer && (bs = bc % 4 ? bs * 64 + buffer : buffer, bc++ % 4)
							? output += String.fromCharCode(255 & bs >> (-2 * bc & 6)) : 0) {
							buffer = chars.indexOf(buffer);
						}
						return output;
					};
				}
				if (typeof globalThis.btoa !== 'function') {
					globalThis.btoa = function(input) {
						var chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';
						var str = String(input);
						var output = '';
						for (var block, charCode, idx = 0, map = chars; str.charAt(idx | 0) || (map = '=', idx % 1);
							output += map.charAt(63 & block >> 8 - idx % 1 * 8)) {
							charCode = str.charCodeAt(idx += 3 / 4);
							if (charCode > 255) throw new Error('btoa range error');
							block = block << 8 | charCode;
						}
						return output;
					};
				}
				if (!globalThis.crypto) globalThis.crypto = {};
				if (typeof globalThis.crypto.getRandomValues !== 'function') {
					globalThis.crypto.getRandomValues = function(arr) {
						for (var i = 0; i < arr.length; i++) arr[i] = (Math.random() * 256) | 0;
						return arr;
					};
				}
				if (typeof globalThis.TextEncoder !== 'function') {
					globalThis.TextEncoder = function() {};
					globalThis.TextEncoder.prototype.encode = function(s) {
						var str = unescape(encodeURIComponent(String(s)));
						var out = new Uint8Array(str.length);
						for (var i = 0; i < str.length; i++) out[i] = str.charCodeAt(i);
						return out;
					};
				}
				if (typeof globalThis.TextDecoder !== 'function') {
					globalThis.TextDecoder = function() {};
					globalThis.TextDecoder.prototype.decode = function(bytes) {
						var str = '';
						for (var i = 0; i < bytes.length; i++) str += String.fromCharCode(bytes[i]);
						try { return decodeURIComponent(escape(str)); } catch (e) { return str; }
					};
				}
				globalThis.document.addEventListener = globalThis.document.addEventListener || function() {};
				globalThis.document.removeEventListener = globalThis.document.removeEventListener || function() {};
				globalThis.document.dispatchEvent = globalThis.document.dispatchEvent || function() { return true; };
				globalThis.document.querySelector = globalThis.document.querySelector || function() { return null; };
				globalThis.document.querySelectorAll = globalThis.document.querySelectorAll || function() { return []; };
				globalThis.window.document = globalThis.document;
				globalThis.history = globalThis.history || { pushState: function(){}, replaceState: function(){}, state: null };
				globalThis.screen = globalThis.screen || { width: 1920, height: 1080 };
				globalThis.localStorage = globalThis.localStorage || {
					getItem: function() { return null; },
					setItem: function() {},
					removeItem: function() {},
					clear: function() {}
				};
				globalThis.sessionStorage = globalThis.sessionStorage || {
					getItem: function() { return null; },
					setItem: function() {},
					removeItem: function() {},
					clear: function() {}
				};
				if (typeof globalThis.fetch !== 'function') {
					globalThis.fetch = function() {
						return Promise.resolve({
							ok: true,
							status: 200,
							headers: { get: function() { return null; } },
							json: function() { return Promise.resolve({}); },
							text: function() { return Promise.resolve(''); },
							arrayBuffer: function() { return Promise.resolve(new ArrayBuffer(0)); }
						});
					};
				}
				if (typeof globalThis.Headers !== 'function') {
					globalThis.Headers = function() {
						this.get = function() { return null; };
						this.set = function() {};
						this.append = function() {};
					};
				}
				if (typeof globalThis.Request !== 'function') {
					globalThis.Request = function(url, init) {
						this.url = url;
						this.method = init && init.method ? init.method : 'GET';
						this.headers = init && init.headers ? init.headers : new Headers();
					};
				}
				if (typeof globalThis.Response !== 'function') {
					globalThis.Response = function(body) {
						this.ok = true;
						this.status = 200;
						this.headers = new Headers();
						this.text = function() { return Promise.resolve(String(body || '')); };
						this.json = function() { return Promise.resolve({}); };
					};
				}
				if (typeof globalThis.MutationObserver !== 'function') {
					globalThis.MutationObserver = function() {
						this.observe = function() {};
						this.disconnect = function() {};
						this.takeRecords = function() { return []; };
					};
				}
				if (typeof globalThis.AbortController !== 'function') {
					globalThis.AbortController = function() {
						this.signal = { aborted: false, addEventListener: function(){}, removeEventListener: function(){} };
						this.abort = function() { this.signal.aborted = true; };
					};
				}
			");
			try
			{
				engine.Execute(bundle);
			}
			catch
			{
				// The secure bundle can throw after exposing Ii; keep going if decrypt entrypoints exist.
			}

			var hasDecryptEntrypoint = false;
			try
			{
				hasDecryptEntrypoint = Convert.ToBoolean(engine.Evaluate("typeof Ii !== 'undefined' && Ii && (typeof Ii.R === 'function' || typeof Ii.D === 'function' || typeof Ii.I === 'function')"));
			}
			catch
			{
				hasDecryptEntrypoint = false;
			}

			if (!hasDecryptEntrypoint)
			{
				_decryptInitError = "entrypoints-missing";
				return false;
			}

			_decryptEngine = engine;
			_decryptEngineInitialized = true;
			return true;
		}
		catch (Exception ex)
		{
			_decryptEngine = null;
			_decryptEngineInitialized = false;
			_decryptInitError = ex.GetType().Name + ":" + ex.Message;
			return false;
		}
	}

	private static bool TryDecryptPayloadLegacy(string encryptedPayload, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(encryptedPayload))
			return false;

		try
		{
			var bytes = ComixToSigner.DecodeBase64Url(encryptedPayload);
			for (var round = 4; round >= 0; round--)
			{
				bytes = ComixToSigner.ReverseRound(round, bytes);
			}

			if (TryNormalizeDecodedBytes(bytes, out json))
				return true;

			if (TryDecompressGzip(bytes, out var inflated) && TryNormalizeDecodedBytes(inflated, out json))
				return true;

			return false;
		}
		catch
		{
			json = string.Empty;
			return false;
		}
	}

	private static bool TryNormalizeDecodedBytes(byte[] bytes, out string json)
	{
		json = string.Empty;
		if (bytes.Length == 0)
			return false;

		try
		{
			var utf8Text = StrictUtf8.GetString(bytes);
			if (TryNormalizeVmDecryption(utf8Text, out json))
				return true;
		}
		catch
		{
		}

		var latin1Text = Encoding.Latin1.GetString(bytes);
		return TryNormalizeVmDecryption(latin1Text, out json);
	}

	private static bool TryDecodeJson(byte[] bytes, out string json)
	{
		json = string.Empty;
		try
		{
			var text = StrictUtf8.GetString(bytes).Trim();
			return TryValidateJson(text, out json);
		}
		catch
		{
			json = string.Empty;
			return false;
		}
	}

	private static bool TryValidateJson(string text, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(text))
			return false;

		try
		{
			using var document = JsonDocument.Parse(text);
			if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
			{
				json = text;
				return true;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool TryDecompressGzip(byte[] bytes, out byte[] decompressed)
	{
		decompressed = [];
		if (bytes.Length < 2 || bytes[0] != 0x1F || bytes[1] != 0x8B)
			return false;

		using var input = new MemoryStream(bytes);
		using var gzip = new GZipStream(input, CompressionMode.Decompress);
		using var output = new MemoryStream();
		gzip.CopyTo(output);
		decompressed = output.ToArray();
		return decompressed.Length > 0;
	}
}
