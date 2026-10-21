# manga-api
The web-api microservice project for the https://manga.index-0.com website.

## Provider harnesses

Run harnesses with `dotnet run --project src/MangaBox.Cli --no-launch-profile -- test <method>`.

- `TestWeebCentral`: reads metadata and complete chapter lists for Magi Craft Meister
  and The Transfer Destination Was a World With Few Pharmacists. Checks the first
  and latest chapters and decodes sample images through the configured `Proxies`
  pool. This harness does not write to the database.
- `ImportWeebCentral`: imports those two manga into the configured database using
  the regular loader. Existing imports are returned without forcing a refresh.

WeebCentral uses the `weebcentral` provider slug and supports series and chapter
URLs. All metadata, full chapter lists, reader fragments, and image downloads use
the existing rotating proxy pool; configure `Proxies:Urls` before running the
live harness or downloading pages. There is no direct HTTP fallback or sticky
proxy affinity. The proxy service still uses the application's shared HTTP/API
services underneath. No web API changes are required.

### WeebCentral rate limits

One shared request scheduler paces all WeebCentral source instances in each
application process, including chapter indexing and image downloads. Defaults:

```json
{
  "WeebCentral": {
    "RequestSpacingSeconds": 2,
    "TransportRetrySeconds": 2,
    "InitialBackoffSeconds": 60,
    "MaxBackoffSeconds": 300,
    "MaxAttempts": 4
  }
}
```

HTTP 429 and server errors retry through the rotating pool
after exponential waits (60, 120, 240 seconds by default). Local backoff is capped
at 300 seconds; a longer server `Retry-After` delta or date is always honored.
The provider pauses all queued requests during backoff instead of immediately
trying other proxies. Responses are disposed before waiting. Cancellation stops
waiting promptly, and the shared cooldown remains after cancellation or attempt
exhaustion so the next chapter cannot trigger another burst. Four attempts means
one initial request and up to three retries. Ordinary errors such as 404 are not
retried. Proxy-pool rate limits also remain in effect.

Connection failures before an HTTP response use shorter transport retry waits
(2, 4, 8 seconds by default, capped at 30 seconds). A SOCKS tunnel reset does not
mean that WeebCentral has rate-limited the application. The rotating proxy pool
excludes endpoints reporting tunnel/connection failures for
`Proxies:FailureCooldownSeconds` (120 seconds by default) and selects healthy
endpoints for retries and subsequent requests. Recovered proxies return after
cooldown. If every endpoint is unavailable, acquisition waits cancellably for a
recovery window instead of repeatedly contacting them. Cancellation does not
quarantine an endpoint, and HTTP responses such as 429 do not mark its tunnel as
broken. Proxy/connection failures are logged as warnings; a failed attempt can
still recover through another proxy within the provider's retry budget.

`test TestWeebCentralBackoff` runs the real proxy rotation and provider scheduler
with simulated HTTP responses and virtual time. It checks metadata/image routing,
shared pacing, concurrent sources, exponential delays, both `Retry-After` forms,
retry exhaustion, disposal, and cancellation without network or database access.
It also checks fast image recovery from typed tunnel/connection errors.
`test TestProxyFailover` exercises both affinity and normal rotating proxy health,
recovery, all-endpoints-unavailable waits, and cancellation, including a local
SOCKS reset check. `test TestWeebCentralImage` downloads and decodes the image from
the reported tunnel-reset log through the real rotating proxy and retry path;
pass `--url <image-url>` to check another image. Neither command writes manga or
image records to the database.

## MangaKatana refresh diagnostics

`test TestMangaKatana` selects a stored MangaKatana chapter and measures direct
HTML, provider refresh, and one image download. Pass `--url <chapter-url>` to
choose a chapter. It also compares read-only cache-bust query plans and runs the
production replacement SQL in temporary tables inside a rolled-back transaction.
It does not change stored manga, images, or cache files.

MangaKatana previously always used a browser with media enabled. A healthy request
took 4.52 seconds versus 0.86 seconds for direct HTTP in the initial measurement;
failures could add repeated 30-second attempts and 15–35-second retry waits.
The provider now requests fresh HTML through `IApiService` first, then makes one
20-second browser fallback with media disabled if content is blocked or missing.
Source timings and fallback decisions are logged. No page HTML cache is reused.

Image-ID replacement also searched every image's slice array for every busted
page. Slice lookup now uses the original manga and skips empty arrays, allowing
the existing manga index to limit the search. This follows the restitcher's rule
that slices belong to the original manga's chapter. Cover and slice references
are still repaired atomically. A chapter refresh continues to persist and queue
each page, and image downloads remain subject to the provider's rate limiter.

## Multiple image cache roots

`Imaging:CacheDir` remains the primary write location. Configure additional read
locations in priority order with `Imaging:AdditionalCacheDirs`, for example:

```json
{
  "Imaging": {
    "CacheDir": "H:/manga-cache",
    "AdditionalCacheDirs": ["G:/manga-cache", "F:/manga-cache"]
  }
}
```

Relative paths and native absolute paths are supported. For containers, use
paths inside the container and mount each drive there. Restart the application
after changing its primary root. Omitting the additional list preserves existing
single-directory behavior.

Reads check the primary first, then each additional root in order. A compressed
primary copy wins over an uncompressed secondary copy. Within one root, a raw
`.dat` file takes priority over a `.gz` file. New downloads, compressed images,
restitches, external metadata, and ZIP files are always saved to the primary.
Missing secondary roots are skipped and are never created by normal reads.

Move whole `<manga-guid>` directories between roots, retaining their `pages` and
`covers` layout. The relative key remains the manga ID plus the hash of the image
URL; no physical location is stored in the database. External images use the
`external` directory and must move with their `.json` metadata sidecars. ZIPs use
the `zips` directory. Completed ZIP lookups do not retain physical paths in memory.

Cache busting removes every raw/compressed copy from all configured roots,
including duplicates and files without complete image metadata. It waits for
active readers; configured roots need deletion permission for invalidation.
`compress-cache` still defaults to the primary; use `--directory` to maintain a
specific archive explicitly.

Run `test TestImageCacheRoots` for an isolated filesystem harness covering root
order, raw/compressed pages and covers, directory relocation, metadata pairing,
ZIP rediscovery, concurrent external downloads, primary-only raw/compressed page
writes, and invalidation while a reader is open. It creates and removes its own
scratch directory under `temp-env`, uses in-memory HTTP responses, and rejects
unexpected database access. Existing cache files are not touched.
