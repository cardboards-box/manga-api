'use strict';
// Navigate directly to a known chapter reading page to capture the chapter-detail signing
const { chromium } = require('playwright');
const fs = require('fs');

// Chapter 9343749 belongs to 8w6dm manga.
// Try the chapter reader URL format: /title/{mangaId}-{slug}/{chapterId}-chapter-{num}
// We don't know the slug/num but can try /chapter/{id} or /read/{id}
const CHAPTER_IDS_TO_TRY = ['9343749', '8882132'];

(async () => {
  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();

  // Try to find the correct chapter URL from the manga's chapter list
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

  const allRequests = [];
  page.on('request', req => allRequests.push(req.url()));

  // First, get all API calls from a manga page to find chapter detail patterns
  console.log('Step 1: Get chapter list for 8w6dm to find a chapter slug...');
  await page.goto('https://comix.to/title/8w6dm', { waitUntil: 'networkidle', timeout: 20000 });
  await page.waitForTimeout(2000);

  // Look at the page source for chapter links
  const pageInfo = await page.evaluate(() => {
	// Try to find chapter data in Vue/React store or window variables
	const scripts = Array.from(document.scripts).map(s => s.src || (s.textContent || '').slice(0,100));
	const allText = document.body.innerHTML;
	const chapterMatches = allText.match(/href="[^"]*chapter[^"]*"/g) || [];
	const titleMatches = allText.match(/\/title\/[a-z0-9]+-[^"'\s]+/g) || [];
	return { 
	  chapterLinks: chapterMatches.slice(0,10),
	  titleLinks: titleMatches.slice(0,5),
	  windowKeys: Object.keys(window).filter(k => k.startsWith('__') || k.includes('app') || k.includes('store')),
	};
  });
  console.log('Chapter links in HTML:', pageInfo.chapterLinks);
  console.log('Title pattern links:', pageInfo.titleLinks);
  console.log('Window keys:', pageInfo.windowKeys.slice(0,15));

  // Check if the chapters API returned anything with chapterId
  const chapReqs = allRequests.filter(u => u.includes('/chapters'));
  console.log('\nChapter-related API requests:', chapReqs);

  // Try direct chapter URLs  
  const chapterUrlFormats = [
	'https://comix.to/chapter/9343749',
	'https://comix.to/read/9343749',
	'https://comix.to/title/8w6dm/9343749',
	'https://comix.to/title/8w6dm/chapter/9343749',
  ];

  for (const url of chapterUrlFormats) {
	try {
	  console.log(`\nTrying: ${url}`);
	  await page.evaluate(() => { window.__signingEvents = []; });
	  const preReqCount = allRequests.length;
	  const resp = await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 10000 });
	  await page.waitForTimeout(2000);

	  const newReqs = allRequests.slice(preReqCount).filter(u => u.includes('/api/'));
	  const events = await page.evaluate(() => window.__signingEvents || []);
	  const finalUrl = page.url();

	  console.log(`  Final URL: ${finalUrl}`);
	  console.log(`  New API requests: ${newReqs.length}`);
	  newReqs.forEach(r => console.log('   ', r));
	  if (events.length > 0) {
		console.log(`  Signing events: ${events.length}`);
		events.forEach(e => {
		  const tok = e.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
		  console.log(`   len=${e.len} var=${e.bytes.slice(49,54)} tok=${tok}`);
		});
		fs.writeFileSync('chapter-detail-capture.json', JSON.stringify({ url, finalUrl, newReqs, events }, null, 2));
		console.log('  -> CAPTURED!');
		break;
	  }
	} catch(e) {
	  console.log(`  Error: ${e.message.slice(0,60)}`);
	}
  }

  // If none worked, look at the actual URL pattern from the HTML we have
  const allLinks = await page.evaluate(() =>
	Array.from(document.querySelectorAll('a[href]')).map(a => a.href).filter(h => h.includes('comix.to'))
  );
  console.log('\nAll comix.to links on current page:', allLinks.slice(0,10));

  await browser.close();
})();
