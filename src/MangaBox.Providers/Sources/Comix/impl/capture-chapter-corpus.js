'use strict';
// Capture many chapter-detail tokens to analyze the signing structure
const { chromium } = require('playwright');
const fs = require('fs');

// Get chapter URLs by visiting several manga pages
const MANGA_IDS = ['8w6dm', '55kym', 'qqwrm', 'n93ny', 'gxgm9', '80d0m', '2zxnk'];

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();

  const captured = [];

  for (const mangaId of MANGA_IDS) {
	if (captured.length >= 30) break;

	// First get chapter links from the manga page
	const listPage = await context.newPage();
	let chapterLinks = [];
	try {
	  await listPage.goto(`https://comix.to/title/${mangaId}`, { waitUntil: 'networkidle', timeout: 15000 });
	  await listPage.waitForTimeout(1500);
	  chapterLinks = await listPage.evaluate(() =>
		Array.from(document.querySelectorAll('a[href*="-chapter-"]'))
		  .slice(0, 5)
		  .map(a => a.href)
	  );
	} catch(e) { /* ignore */ }
	await listPage.close();

	console.log(`${mangaId}: ${chapterLinks.length} chapter links`);

	for (const chUrl of chapterLinks.slice(0, 3)) {
	  const chPage = await context.newPage();
	  try {
		await chPage.addInitScript(() => {
		  window.__signingEvents = [];
		  const orig = window.btoa;
		  window.btoa = function(s) {
			const r = orig.call(this, s);
			const bytes = Array.from(s).map(c => c.charCodeAt(0));
			window.__signingEvents.push({ bytes, result: r, len: bytes.length });
			return r;
		  };
		});

		const chapterReqs = [];
		chPage.on('request', req => {
		  const u = req.url();
		  if (u.match(/\/api\/v1\/chapters\/\d+\?/)) chapterReqs.push(u);
		});

		await chPage.goto(chUrl, { waitUntil: 'networkidle', timeout: 20000 });
		await chPage.waitForTimeout(2000);

		const events = await chPage.evaluate(() => window.__signingEvents || []);

		for (const req of chapterReqs) {
		  const m = req.match(/\/api\/v1\/chapters\/(\d+)\?.*_=([^&]+)/);
		  if (!m) continue;
		  const chapterId = m[1];
		  const token = m[2];

		  // Find the matching signing event (59-byte)
		  const ev = events.find(e => {
			const eTok = e.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
			return eTok === token;
		  }) || events.find(e => e.len < 65);

		  if (ev) {
			captured.push({ mangaId, chapterId, token, bytes: ev.bytes, len: ev.len });
			console.log(`  ✓ chapter ${chapterId} (${ev.len}B) var=${JSON.stringify(ev.bytes.slice(35,57))}`);
		  }
		}
	  } catch(e) {
		console.log(`  Error: ${e.message.slice(0,50)}`);
	  }
	  await chPage.close();
	  if (captured.length >= 30) break;
	}
  }

  fs.writeFileSync('chapter-corpus.json', JSON.stringify(captured, null, 2));
  console.log(`\nCaptured ${captured.length} chapter tokens`);
  await browser.close();
})();
