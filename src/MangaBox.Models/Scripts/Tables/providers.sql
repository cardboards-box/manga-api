CREATE TABLE IF NOT EXISTS mb_providers (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	name TEXT NOT NULL UNIQUE,
	url TEXT NOT NULL,
	type INTEGER NOT NULL,
	enabled BOOLEAN NOT NULL DEFAULT TRUE,
	home_url TEXT NOT NULL,
	referrer TEXT,
	
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP
);


WITH providers_base (name, url, type, enabled, home_url, referrer) AS (
	VALUES
		('MangaDex', 'https://api.mangadex.org', 0, TRUE, 'https://mangadex.org', NULL),
		('MangaKatana', 'https://mangakatana.com', 1, FALSE, 'https://mangakatana.com', 'https://mangakatana.com/'),
		('MangaClash', 'https://mangaclash.com', 1, FALSE, 'https://mangaclash.com', NULL),
		('nhentai.to', 'https://nhentai.to', 1, FALSE, 'https://nhentai.to/', 'https://nhentai.to/'),
		('DarkScans', 'https://dark-scan.com', 1, FALSE, 'https://dark-scan.com', NULL),

		('Mangakakalot.tv', 'https://ww4.mangakakalot.tv', 1, FALSE, 'https://ww4.mangakakalot.tv', NULL),
		('MangaKakalot.com', 'https://mangakakalot.com', 1, FALSE, 'https://mangakakalot.com', 'https://mangakakalot.com/'),
		('MangaKakalot.com (Alt)', 'https://mangakakalot.com', 1, FALSE, 'https://mangakakalot.com', 'https://mangakakalot.com/')
)
INSERT INTO mb_providers (name, url, type, enabled, home_url, referrer)
SELECT
	s.name, s.url, s.type, s.enabled, s.home_url, s.referrer
FROM providers_base s
LEFT JOIN mb_providers t ON s.name = t.name
WHERE t.id IS NULL;