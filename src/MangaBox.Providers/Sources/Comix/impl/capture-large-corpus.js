'use strict';
// Capture a large corpus of 5-char IDs to fully determine the 40-bit mask system
// We need at least 40 linearly independent samples; target 80+ to be safe
const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
  const page = await context.newPage();

  // Intercept btoa globally
  await page.addInitScript(() => {
	window.__signingEvents = [];
	const origBtoa = window.btoa;
	window.btoa = function(s) {
	  const result = origBtoa.call(this, s);
	  const bytes = Array.from(s).map(c => c.charCodeAt(0));
	  if (bytes.length >= 60 && bytes.length <= 70) {
		window.__signingEvents.push({ bytes, result, t: Date.now() });
	  }
	  return result;
	};
	// Also hook XHR to correlate token with manga ID
	const origOpen = XMLHttpRequest.prototype.open;
	XMLHttpRequest.prototype.open = function(method, url, ...rest) {
	  if (typeof url === 'string' && url.includes('/chapters') && url.includes('_=')) {
		this.__captureUrl = url;
	  }
	  return origOpen.call(this, method, url, ...rest);
	};
  });

  const captured = new Map(); // id -> { bytes, token }

  async function captureForId(id) {
	if (captured.has(id)) return;
	const newPage = await context.newPage();
	try {
	  await newPage.addInitScript(() => {
		window.__signingEvents = [];
		const origBtoa = window.btoa;
		window.btoa = function(s) {
		  const result = origBtoa.call(this, s);
		  const bytes = Array.from(s).map(c => c.charCodeAt(0));
		  if (bytes.length >= 60 && bytes.length <= 70) {
			window.__signingEvents.push({ bytes, result, t: Date.now() });
		  }
		  return result;
		};
	  });

	  let token = null;
	  newPage.on('request', req => {
		const url = req.url();
		if (url.includes(`/${id}/chapters`) && url.includes('_=')) {
		  const m = url.match(/_=([^&]+)/);
		  if (m) token = m[1];
		}
	  });

	  await newPage.goto(`https://comix.to/manga/${id}`, { waitUntil: 'networkidle', timeout: 20000 });
	  await newPage.waitForTimeout(2000);

	  if (!token) {
		await newPage.evaluate(() => window.scrollTo(0, 300));
		await newPage.waitForTimeout(2000);
	  }

	  const events = await newPage.evaluate(() => window.__signingEvents || []);

	  if (token && events.length > 0) {
		// Find the matching signing event
		const b64 = token.replace(/-/g,'+').replace(/_/g,'/');
		const padded = b64 + '='.repeat((4 - b64.length % 4) % 4);
		const tokenBytes = Array.from(Buffer.from(padded, 'base64'));

		// Match by result bytes
		let matched = null;
		for (const ev of events) {
		  const evB64 = ev.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
		  if (evB64 === token || ev.result.replace(/=+$/,'') === b64) {
			matched = ev;
			break;
		  }
		}
		if (!matched && events.length > 0) matched = events[events.length - 1];

		if (matched) {
		  console.log(`✓ ${id} (${id.length} chars, ${matched.bytes.length} bytes)`);
		  captured.set(id, { id, bytes: matched.bytes, token });
		} else {
		  console.log(`? ${id} - token captured but no btoa match`);
		  if (token) captured.set(id, { id, bytes: tokenBytes, token });
		}
	  } else {
		console.log(`✗ ${id} - no token captured (token=${token}, events=${events.length})`);
	  }
	} catch(e) {
	  console.log(`✗ ${id} error:`, e.message.slice(0,60));
	} finally {
	  await newPage.close();
	}
  }

  // First, get a large list of IDs from the homepage and multiple category/list pages
  const allIds = new Set();

  const seedPages = [
	'https://comix.to/',
	'https://comix.to/comics?page=1',
	'https://comix.to/comics?page=2',
	'https://comix.to/comics?page=3',
	'https://comix.to/new',
	'https://comix.to/popular',
  ];

  for (const seedUrl of seedPages) {
	try {
	  await page.goto(seedUrl, { waitUntil: 'networkidle', timeout: 20000 });
	  await page.waitForTimeout(1000);
	  const ids = await page.evaluate(() => {
		const links = Array.from(document.querySelectorAll('a[href*="/manga/"]'));
		return links.map(a => {
		  const m = a.href.match(/\/manga\/([a-z0-9]+)/);
		  return m ? m[1] : null;
		}).filter(Boolean);
	  });
	  ids.forEach(id => allIds.add(id));
	  console.log(`Seed ${seedUrl}: +${ids.length} ids, total=${allIds.size}`);
	} catch(e) {
	  console.log(`Seed failed: ${seedUrl}`, e.message.slice(0,40));
	}
  }

  // Focus on 5-char IDs
  const fiveCharIds = [...allIds].filter(id => id.length === 5);
  const otherIds = [...allIds].filter(id => id.length !== 5);
  console.log(`\nTotal IDs: ${allIds.size}, 5-char: ${fiveCharIds.length}, other: ${otherIds.length}`);

  // Capture 5-char IDs first (need at least 40 for full rank)
  for (const id of fiveCharIds.slice(0, 80)) {
	await captureForId(id);
	if (captured.size >= 80) break;
  }

  // Also capture some 4-char IDs
  const fourCharIds = otherIds.filter(id => id.length === 4);
  for (const id of fourCharIds.slice(0, 20)) {
	await captureForId(id);
  }

  const results = [...captured.values()];
  fs.writeFileSync('large-corpus.json', JSON.stringify(results, null, 2));
  console.log(`\nDone! Captured ${results.length} total (${results.filter(r=>r.id.length===5).length} five-char, ${results.filter(r=>r.id.length===4).length} four-char)`);

  await browser.close();
})();
