'use strict';
// Find a manga with visible chapter links and capture chapter-detail signing
const { chromium } = require('playwright');
const fs = require('fs');

const corpus = require('./merged-corpus.json');
// Use a well-known ID with chapters
const MANGA_IDS = ['55kym', 'qqwrm', 'n93ny', 'gxgm9', '80d0m'];

(async () => {
  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();

  for (const mangaId of MANGA_IDS) {
	const page = await context.newPage();

	await page.addInitScript(() => {
	  window.__signingEvents = [];
	  const orig = window.btoa;
	  window.btoa = function(s) {
		const r = orig.call(this, s);
		const bytes = Array.from(s).map(c => c.charCodeAt(0));
		window.__signingEvents.push({ bytes, result: r, len: bytes.length });
		return r;
	  };
	});

	const requests = [];
	page.on('request', req => {
	  const url = req.url();
	  if (url.includes('comix.to/api')) requests.push(url);
	});

	await page.goto(`https://comix.to/title/${mangaId}`, { waitUntil: 'networkidle', timeout: 20000 });
	await page.waitForTimeout(2000);

	// Check for chapter links - try different selectors
	const linkInfo = await page.evaluate(() => {
	  const hrefs = Array.from(document.querySelectorAll('a[href]'))
		.map(a => a.href)
		.filter(h => h.includes('/chapter') || h.includes('/read'))
		.slice(0, 10);
	  return { hrefs, title: document.title };
	});
	console.log(`\n${mangaId}: "${linkInfo.title}" - chapter links: ${linkInfo.hrefs.length}`);
	linkInfo.hrefs.forEach(h => console.log('  ', h));

	if (linkInfo.hrefs.length > 0) {
	  // Navigate to first chapter
	  await page.evaluate(() => window.__signingEvents = []);
	  const chUrl = linkInfo.hrefs[0];
	  console.log(`Navigating to chapter: ${chUrl}`);
	  await page.goto(chUrl, { waitUntil: 'networkidle', timeout: 25000 });
	  await page.waitForTimeout(3000);

	  const events = await page.evaluate(() => window.__signingEvents || []);
	  console.log(`Chapter signing events: ${events.length}`);

	  const chapterReqs = requests.filter(u => u.match(/\/api\/v1\/chapters\/\d/));
	  const chapterListReqs = requests.filter(u => u.includes('/chapters?'));
	  console.log('Chapter-detail reqs:', chapterReqs.length);
	  chapterReqs.forEach(u => console.log('  ', u));

	  if (events.length > 0 || chapterReqs.length > 0) {
		fs.writeFileSync('chapter-sign-capture.json', JSON.stringify({
		  mangaId, chUrl, chapterReqs, chapterListReqs, signingEvents: events
		}, null, 2));
		console.log('Saved capture!');
		await page.close();
		break;
	  }
	}
	await page.close();
  }

  await browser.close();
})();
