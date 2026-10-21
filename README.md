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
