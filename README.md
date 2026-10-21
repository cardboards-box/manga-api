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
URLs. Its image downloads use the existing proxy pool; configure `Proxies:Urls`
before running the live harness or downloading pages. No web API changes are required.

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
