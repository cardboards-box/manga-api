'use strict';
// Try multiple strategies to get 8w6dm chapters request to fire
const { chromium } = require('playwright');
const fs = require('fs');

const TARGET = '8w6dm';

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
	  if (bytes.length >= 60 && bytes.length <= 70) window.__signingEvents.push({ bytes, result: r });
	  return r;
	};
  });

  const urls = [];
  page.on('request', req => {
	const url = req.url();
	if (url.includes('comix.to/api') || url.includes('/chapters')) {
	  urls.push(url);
	}
  });

  console.log('Navigating to homepage first...');
  await page.goto('https://comix.to/', { waitUntil: 'networkidle', timeout: 20000 });
  await page.waitForTimeout(1000);

  console.log('Navigating to manga page...');
  await page.goto(`https://comix.to/title/${TARGET}`, { waitUntil: 'networkidle', timeout: 20000 });
  await page.waitForTimeout(3000);

  console.log('API calls so far:', urls.filter(u => u.includes(TARGET)).join('\n  '));

  // Try scrolling
  for (let i = 0; i < 5; i++) {
	await page.evaluate(i => window.scrollTo(0, i * 200), i);
	await page.waitForTimeout(500);
  }
  await page.waitForTimeout(2000);

  // Check if there are any chapter-related elements
  const pageInfo = await page.evaluate(() => ({
	title: document.title,
	chapterEls: document.querySelectorAll('[class*="chapter"]').length,
	links: Array.from(document.querySelectorAll('a[href*="/chapter"]')).slice(0,5).map(a => a.href),
	bodySnippet: document.body.innerText.slice(0, 300),
  }));
  console.log('Page title:', pageInfo.title);
  console.log('Chapter elements:', pageInfo.chapterEls);
  console.log('Chapter links:', pageInfo.links);
  console.log('Body snippet:', pageInfo.bodySnippet.slice(0, 200));

  const targetUrls = urls.filter(u => u.includes(TARGET));
  console.log(`\nAPI calls for ${TARGET}:`, targetUrls.length);
  targetUrls.forEach(u => console.log(' ', u));

  const events = await page.evaluate(() => window.__signingEvents || []);
  console.log('btoa signing events:', events.length);

  // If no chapters request fired, also try the API directly via XHR
  if (targetUrls.filter(u => u.includes('chapters')).length === 0) {
	console.log('\nChapters not loaded automatically. Trying to trigger via eval...');
	await page.evaluate(id => {
	  // Try to trigger via fetch
	  fetch(`/api/v1/manga/${id}/chapters?page=1&limit=20&order%5Bnumber%5D=desc`)
		.then(r => r.json()).then(d => console.log('Direct fetch result:', JSON.stringify(d).slice(0,100)));
	}, TARGET);
	await page.waitForTimeout(3000);
  }

  const finalTargetUrls = urls.filter(u => u.includes(TARGET) && u.includes('chapters'));
  const finalEvents = await page.evaluate(() => window.__signingEvents || []);

  console.log('\nFinal chapters URLs:', finalTargetUrls);
  console.log('Final signing events:', finalEvents.length);

  if (finalEvents.length > 0) {
	for (const ev of finalEvents) {
	  const tok = ev.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	  console.log('Signing event:', ev.bytes.length, 'bytes, token:', tok);
	  console.log('Var bytes:', ev.bytes.slice(49, 49 + 5));
	}
  }

  fs.writeFileSync(`capture-${TARGET}.json`, JSON.stringify({ urls, events: finalEvents }, null, 2));
  await browser.close();
})();
