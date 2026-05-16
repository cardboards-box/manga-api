'use strict';
// Capture large corpus using /title/ links (correct URL pattern) and API browse
const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
  const page = await context.newPage();

  // Collect IDs from multiple pages using the /title/ link pattern
  const allIds = new Set();

  // Listen for API browse responses to get more IDs
  page.on('response', async res => {
	const url = res.url();
	if (url.includes('/api/v1/') && (url.includes('manga') || url.includes('comics') || url.includes('titles') || url.includes('browse'))) {
	  try {
		const text = await res.text();
		const matches = text.match(/['"](\/title\/|id['"]\s*:\s*['"]{1})([a-z0-9]{4,6})/g) || [];
		if (matches.length) console.log('API response from', url.slice(0,60), '- found', matches.length, 'potential IDs');
	  } catch(e) {}
	}
  });

  const seedPages = [
	'https://comix.to/',
	'https://comix.to/browse',
	'https://comix.to/browse?sort=views_7d%3Adesc',
	'https://comix.to/browse?sort=created_at%3Adesc',
	'https://comix.to/browse?page=2',
	'https://comix.to/browse?page=3',
	'https://comix.to/browse?page=4',
	'https://comix.to/browse?page=5',
  ];

  for (const seedUrl of seedPages) {
	try {
	  await page.goto(seedUrl, { waitUntil: 'networkidle', timeout: 25000 });
	  await page.waitForTimeout(2000);
	  const ids = await page.evaluate(() => {
		const links = Array.from(document.querySelectorAll('a[href*="/title/"]'));
		return links.map(a => {
		  const m = a.href.match(/\/title\/([a-z0-9]+)/);
		  return m ? m[1] : null;
		}).filter(Boolean);
	  });
	  ids.forEach(id => allIds.add(id));
	  console.log(`Seed ${seedUrl.slice(0,50)}: +${ids.length}, total=${allIds.size}`);
	} catch(e) {
	  console.log('Seed error:', e.message.slice(0,50));
	}
  }

  const fiveCharIds = [...allIds].filter(id => id.length === 5);
  const fourCharIds = [...allIds].filter(id => id.length === 4);
  const threeCharIds = [...allIds].filter(id => id.length === 3);
  console.log(`\nAll: ${allIds.size} | 3-char: ${threeCharIds.length} | 4-char: ${fourCharIds.length} | 5-char: ${fiveCharIds.length}`);
  console.log('5-char IDs:', fiveCharIds.slice(0, 20).join(', '));

  const captured = [];

  async function captureId(id) {
	const p = await context.newPage();
	try {
	  await p.addInitScript(() => {
		window.__signingEvents = [];
		const orig = window.btoa;
		window.btoa = function(s) {
		  const r = orig.call(this, s);
		  const bytes = Array.from(s).map(c => c.charCodeAt(0));
		  if (bytes.length >= 60 && bytes.length <= 70) window.__signingEvents.push({ bytes, result: r });
		  return r;
		};
	  });
	  let token = null;
	  p.on('request', req => {
		const url = req.url();
		if (url.includes(`/${id}/chapters`) && url.includes('_=')) {
		  const m = url.match(/_=([^&]+)/); if (m) token = m[1];
		}
	  });
	  await p.goto(`https://comix.to/title/${id}`, { waitUntil: 'networkidle', timeout: 20000 });
	  await p.waitForTimeout(2000);
	  if (!token) { await p.evaluate(() => window.scrollTo(0, 400)); await p.waitForTimeout(2000); }

	  const events = await p.evaluate(() => window.__signingEvents || []);
	  if (token && events.length > 0) {
		// Find best matching event
		let best = events[events.length - 1];
		for (const ev of events) {
		  const evTok = ev.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
		  if (evTok === token) { best = ev; break; }
		}
		captured.push({ id, bytes: best.bytes, token });
		console.log(`✓ ${id} (${id.length}-char, ${best.bytes.length}B)`);
	  } else {
		console.log(`✗ ${id} (token=${token ? 'yes':'no'}, events=${events.length})`);
	  }
	} catch(e) {
	  console.log(`✗ ${id} err: ${e.message.slice(0,50)}`);
	} finally {
	  await p.close();
	}
  }

  // Capture 5-char first (need 40+ for full rank), then 4-char, then 3-char
  const targets = [
	...fiveCharIds.slice(0, 80),
	...fourCharIds.slice(0, 30),
	...threeCharIds.slice(0, 20),
  ];

  for (const id of targets) {
	await captureId(id);
  }

  fs.writeFileSync('large-corpus.json', JSON.stringify(captured, null, 2));
  const s5 = captured.filter(r => r.id.length === 5).length;
  const s4 = captured.filter(r => r.id.length === 4).length;
  const s3 = captured.filter(r => r.id.length === 3).length;
  console.log(`\nDone! ${captured.length} total | 5-char:${s5} 4-char:${s4} 3-char:${s3}`);
  await browser.close();
})();
