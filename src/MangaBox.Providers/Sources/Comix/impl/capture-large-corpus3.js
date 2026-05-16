'use strict';
// Capture 5-char corpus with incremental save - won't lose data on crash
const { chromium } = require('playwright');
const fs = require('fs');

const OUTFILE = 'large-corpus.json';
const LOGFILE = 'large-corpus3-log.txt';
const log = (msg) => { console.log(msg); fs.appendFileSync(LOGFILE, msg + '\n'); };

// Load existing corpus to resume
let captured = [];
if (fs.existsSync(OUTFILE)) {
  try { captured = JSON.parse(fs.readFileSync(OUTFILE)); } catch(e) {}
}
const capturedIds = new Set(captured.map(c => c.id));
log(`Resuming with ${captured.length} already captured IDs`);

(async () => {
  const browser = await chromium.launch({ headless: true }); // headless to be faster
  const context = await browser.newContext();

  // Get seed IDs from a single browse page using the main page
  const page = await context.newPage();
  const allIds = new Set();

  const seedPages = [
	'https://comix.to/',
	'https://comix.to/browse',
	'https://comix.to/browse?sort=views_7d%3Adesc',
	'https://comix.to/browse?sort=created_at%3Adesc',
	'https://comix.to/browse?page=2',
	'https://comix.to/browse?page=3',
	'https://comix.to/browse?page=4',
	'https://comix.to/browse?page=5',
	'https://comix.to/browse?page=6',
	'https://comix.to/browse?page=7',
  ];

  for (const seedUrl of seedPages) {
	try {
	  await page.goto(seedUrl, { waitUntil: 'networkidle', timeout: 25000 });
	  await page.waitForTimeout(1500);
	  const ids = await page.evaluate(() =>
		Array.from(document.querySelectorAll('a[href*="/title/"]'))
		  .map(a => { const m = a.href.match(/\/title\/([a-z0-9]+)/); return m ? m[1] : null; })
		  .filter(Boolean)
	  );
	  ids.forEach(id => allIds.add(id));
	  log(`Seed ${seedUrl.slice(20,50)}: +${ids.length}, total=${allIds.size}`);
	} catch(e) { log(`Seed error: ${e.message.slice(0,60)}`); }
  }
  await page.close();

  const fiveCharIds = [...allIds].filter(id => id.length === 5 && !capturedIds.has(id));
  const fourCharIds = [...allIds].filter(id => id.length === 4 && !capturedIds.has(id));
  log(`\nTo capture: 5-char=${fiveCharIds.length}, 4-char=${fourCharIds.length}`);
  log('Including known target: 8w6dm in list: ' + fiveCharIds.includes('8w6dm'));

  // Add 8w6dm explicitly if not found
  if (!capturedIds.has('8w6dm') && !fiveCharIds.includes('8w6dm')) {
	fiveCharIds.push('8w6dm');
	log('Added 8w6dm explicitly');
  }

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
		let best = events[events.length - 1];
		for (const ev of events) {
		  const evTok = ev.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
		  if (evTok === token) { best = ev; break; }
		}
		const entry = { id, bytes: best.bytes, token };
		captured.push(entry);
		capturedIds.add(id);
		// Save after every capture
		fs.writeFileSync(OUTFILE, JSON.stringify(captured, null, 2));
		log(`✓ ${id} (${id.length}-char, ${best.bytes.length}B) total=${captured.length}`);
		return true;
	  } else {
		log(`✗ ${id} (token=${token ? 'yes':'no'}, events=${events.length})`);
		return false;
	  }
	} catch(e) {
	  log(`✗ ${id} err: ${e.message.slice(0,60)}`);
	  return false;
	} finally {
	  await p.close();
	}
  }

  // Capture 5-char IDs
  let success5 = captured.filter(c => c.id.length === 5).length;
  for (const id of fiveCharIds) {
	if (success5 >= 80) break;
	const ok = await captureId(id);
	if (ok) success5++;
  }

  // Capture 4-char IDs
  let success4 = captured.filter(c => c.id.length === 4).length;
  for (const id of fourCharIds) {
	if (success4 >= 20) break;
	const ok = await captureId(id);
	if (ok) success4++;
  }

  const s5 = captured.filter(r => r.id.length === 5).length;
  const s4 = captured.filter(r => r.id.length === 4).length;
  log(`\nDone! ${captured.length} total | 5-char:${s5} 4-char:${s4}`);
  await browser.close();
})();
