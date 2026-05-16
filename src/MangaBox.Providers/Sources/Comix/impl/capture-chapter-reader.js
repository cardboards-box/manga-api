'use strict';
// Navigate to actual chapter reader URL and capture signing events
const { chromium } = require('playwright');
const fs = require('fs');

// Known chapter URLs from the manga page HTML
const CHAPTER_URLS = [
  'https://comix.to/title/8w6dm-i-saved-you-but-im-not-responsible/8919603-chapter-75',
  'https://comix.to/title/8w6dm-i-saved-you-but-im-not-responsible/8880855-chapter-74',
  'https://comix.to/title/8w6dm-i-saved-you-but-im-not-responsible/8849495-chapter-73',
];

(async () => {
  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
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

  const allApiReqs = [];
  page.on('request', req => {
	if (req.url().includes('comix.to/api')) allApiReqs.push(req.url());
  });

  for (const chUrl of CHAPTER_URLS) {
	console.log(`\nNavigating to: ${chUrl}`);
	await page.evaluate(() => { window.__signingEvents = []; });
	const preCount = allApiReqs.length;

	await page.goto(chUrl, { waitUntil: 'networkidle', timeout: 25000 });
	await page.waitForTimeout(3000);

	const newReqs = allApiReqs.slice(preCount);
	const events = await page.evaluate(() => window.__signingEvents || []);

	console.log(`API requests: ${newReqs.length}`);
	newReqs.forEach(u => console.log('  ', u.slice(0, 120)));
	console.log(`Signing events: ${events.length}`);
	events.forEach(e => {
	  const tok = e.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	  console.log(`  len=${e.len} var=${JSON.stringify(e.bytes.slice(49,54))} tok=${tok}`);
	});

	const chapterReqs = newReqs.filter(u => u.match(/\/api\/v1\/chapters\/\d/));
	if (chapterReqs.length > 0) {
	  console.log('\n-> Chapter detail requests found!');
	  fs.writeFileSync('chapter-detail-capture.json', JSON.stringify({
		chUrl, chapterReqs, signingEvents: events, allReqs: newReqs
	  }, null, 2));
	  break;
	}
  }

  await browser.close();
})();
