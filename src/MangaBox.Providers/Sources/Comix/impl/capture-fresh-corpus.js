'use strict';
// Capture btoa input bytes + token for many IDs in a single browser session
// This gives us a fresh key's full byte layout to solve the signer
const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext();
  const page = await ctx.newPage();

  await page.addInitScript(() => {
	window.__rawTokens = [];
	const origBtoa = window.btoa;
	window.btoa = function(data) {
	  const result = origBtoa.call(window, data);
	  if (result.length > 80) {
		const bytes = [...data].map(c => c.charCodeAt(0));
		window.__rawTokens.push({ bytes, b64: result });
	  }
	  return result;
	};
	window.__xhrTokens = [];
	const origOpen = XMLHttpRequest.prototype.open;
	XMLHttpRequest.prototype.open = function(method, url) {
	  if (typeof url === 'string' && url.includes('/manga/') && url.includes('/chapters?')) {
		const m = url.match(/manga\/([^/]+)\/chapters/);
		if (m) window.__xhrTokens.push({ id: m[1], url });
	  }
	  return origOpen.apply(this, arguments);
	};
  });

  const allData = [];

  const homepageIds = [];
  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(3000);
  const links = await page.$$eval('a[href*="/title/"]', els =>
	[...new Set(els.map(el => el.href.match(/\/title\/([a-z0-9]+)/)?.[1]).filter(Boolean))]
  );
  homepageIds.push(...links.slice(0, 30));
  process.stderr.write('Found ' + homepageIds.length + ' IDs from homepage\n');

  // Navigate to each title page and capture the btoa call + xhr
  for (const id of homepageIds) {
	// Reset captured data
	await page.evaluate(() => { window.__rawTokens = []; window.__xhrTokens = []; });

	try {
	  await page.goto('https://comix.to/title/' + id, { waitUntil: 'domcontentloaded', timeout: 20000 });
	  await page.waitForTimeout(2500);

	  const { rawTokens, xhrTokens } = await page.evaluate(() => ({
		rawTokens: window.__rawTokens,
		xhrTokens: window.__xhrTokens
	  }));

	  // Match up btoa output with xhr token
	  for (const xhr of xhrTokens) {
		// Find matching btoa call
		for (const rt of rawTokens) {
		  // convert standard b64 -> b64url for comparison
		  const b64url = rt.b64.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
		  const m = xhr.url.match(/[?&]_=([\w-]+)/);
		  if (m && m[1] === b64url) {
			allData.push({ id: xhr.id, bytes: rt.bytes, token: m[1] });
			process.stderr.write('Captured: ' + xhr.id + ' (' + rt.bytes.length + ' bytes)\n');
			break;
		  }
		}
	  }
	} catch(e) {
	  process.stderr.write('Error for ' + id + ': ' + e.message + '\n');
	}
  }

  await browser.close();

  // Write results
  fs.writeFileSync('fresh-corpus.json', JSON.stringify(allData, null, 2));
  process.stderr.write('\nTotal captured: ' + allData.length + '\n');

  // Emit the data for solving
  for (const d of allData) {
	console.log(JSON.stringify({ id: d.id, bytes: d.bytes, token: d.token }));
  }
})();
