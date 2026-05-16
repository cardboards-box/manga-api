'use strict';
// Capture the actual browser signing for /api/v1/chapters/{id} requests
const { chromium } = require('playwright');
const fs = require('fs');

const TARGET_CHAPTER = '9343749'; // from the failing URL
const TARGET_MANGA = '8w6dm';     // manga this chapter belongs to

(async () => {
  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
  const page = await context.newPage();

  await page.addInitScript(() => {
	window.__signingEvents = [];
	window.__allRequests = [];
	const orig = window.btoa;
	window.btoa = function(s) {
	  const r = orig.call(this, s);
	  const bytes = Array.from(s).map(c => c.charCodeAt(0));
	  window.__signingEvents.push({ bytes, result: r, len: bytes.length, stack: new Error().stack.split('\n').slice(1,3).join(' | ') });
	  return r;
	};
  });

  const requests = [];
  page.on('request', req => {
	const url = req.url();
	if (url.includes('comix.to/api')) requests.push(url);
  });

  // Navigate to the manga page first to load chapter list
  console.log(`\nNavigating to manga page: /title/${TARGET_MANGA}`);
  await page.goto(`https://comix.to/title/${TARGET_MANGA}`, { waitUntil: 'networkidle', timeout: 25000 });
  await page.waitForTimeout(2000);

  const events1 = await page.evaluate(() => window.__signingEvents || []);
  console.log(`Signing events after manga page: ${events1.length}`);
  events1.forEach(e => {
	const tok = e.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	console.log(`  len=${e.len} bytes[49..54]=${e.bytes.slice(49,54)} token=${tok.slice(56)}`);
  });

  // Now click the first chapter link to navigate to a chapter page
  const chapterLinks = await page.evaluate(() =>
	Array.from(document.querySelectorAll('a[href*="/chapter"]')).slice(0, 5).map(a => a.href)
  );
  console.log(`\nChapter links found: ${chapterLinks.length}`);
  chapterLinks.forEach(l => console.log(' ', l));

  if (chapterLinks.length > 0) {
	await page.evaluate(() => window.__signingEvents = []);
	console.log('\nNavigating to first chapter...');
	await page.goto(chapterLinks[0], { waitUntil: 'networkidle', timeout: 25000 });
	await page.waitForTimeout(3000);

	const events2 = await page.evaluate(() => window.__signingEvents || []);
	console.log(`Signing events after chapter page: ${events2.length}`);
	events2.forEach(e => {
	  const tok = e.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	  console.log(`  len=${e.len} bytes[49..54]=${e.bytes.slice(49,54)} token=${tok.slice(0,20)}...${tok.slice(56)}`);
	});
  }

  // Log all API requests intercepted
  const chapterDetailReqs = requests.filter(u => u.match(/\/api\/v1\/chapters\/\d/));
  const chapterListReqs = requests.filter(u => u.includes('/chapters?'));
  console.log(`\nChapter-detail API requests (${chapterDetailReqs.length}):`);
  chapterDetailReqs.forEach(u => console.log(' ', u));
  console.log(`\nChapter-list API requests (${chapterListReqs.length}):`);
  chapterListReqs.forEach(u => console.log(' ', u));

  const allEvents = await page.evaluate(() => window.__signingEvents || []);
  fs.writeFileSync('chapter-sign-capture.json', JSON.stringify({
	chapterDetailReqs, chapterListReqs, signingEvents: allEvents
  }, null, 2));

  await browser.close();
})();
