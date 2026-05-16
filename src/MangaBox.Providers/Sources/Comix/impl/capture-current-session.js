'use strict';
// Capture additional tokens from the CURRENT session (matching batchB prefix/suffix)
// to supplement the 28 batchB tokens to get >= 40 long + >= 32 short
const { chromium } = require('playwright');

const KNOWN_SESSION_TOKEN_55KYM = 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const tokens = new Map();
  let sessionVerified = false;

  page.on('request', req => {
	const url = req.url();
	const m = url.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?[^#]*[?&]_=([\w\-]+)/);
	if (m && !tokens.has(m[1])) {
	  // Verify this is the same session on first 55kym hit
	  if (m[1] === '55kym' && !sessionVerified) {
		if (m[2] !== KNOWN_SESSION_TOKEN_55KYM) {
		  process.stderr.write('SESSION MISMATCH for 55kym! Keys have rotated.\n');
		  process.stderr.write('Got: ' + m[2] + '\n');
		  process.stderr.write('Expected: ' + KNOWN_SESSION_TOKEN_55KYM + '\n');
		} else {
		  process.stderr.write('Session verified - same key set as batchB\n');
		  sessionVerified = true;
		}
	  }
	  tokens.set(m[1], m[2]);
	  process.stderr.write('captured [' + tokens.size + '] ' + m[1] + ' (' + m[1].length + '-char)\n');
	}
  });

  // First go to homepage to get IDs (same as batchB collection run)
  process.stderr.write('Loading homepage...\n');
  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const homeIds = await page.$$eval('a[href*="/title/"]', (as) => {
	const uniq = [], seen = new Set();
	for (const a of as) {
	  const h = a.getAttribute('href') || '';
	  const m = h.match(/\/title\/([a-z0-9]+)/i);
	  if (!m) continue;
	  const id = m[1];
	  if (seen.has(id)) continue;
	  seen.add(id);
	  uniq.push(id);
	}
	return uniq;
  });

  process.stderr.write('Home IDs: ' + homeIds.length + ': ' + homeIds.join(', ') + '\n');

  // Navigate all home IDs to capture tokens
  for (const id of homeIds) {
	if (tokens.size >= 90) break;
	try {
	  await page.goto('https://comix.to/title/' + id, { waitUntil: 'domcontentloaded', timeout: 30000 });
	  await page.waitForTimeout(2500);
	} catch (e) {
	  process.stderr.write('nav error ' + id + ': ' + e.message + '\n');
	}
  }

  // If we didn't get 55kym on the homepage, visit it explicitly
  if (!tokens.has('55kym')) {
	await page.goto('https://comix.to/title/55kym', { waitUntil: 'domcontentloaded', timeout: 30000 });
	await page.waitForTimeout(3000);
  }

  await browser.close();

  const allOracles = [...tokens.entries()];
  process.stderr.write('\nTotal captured: ' + tokens.size + '\n');
  process.stderr.write('Session verified: ' + sessionVerified + '\n');

  // Print all captured tokens for pasting into solve-split.js
  process.stderr.write('\n--- ALL CAPTURED TOKENS ---\n');
  for (const [id, tok] of allOracles) {
	process.stderr.write("  ['" + id + "', '" + tok + "'],\n");
  }

  // Quick summary by length
  const short = allOracles.filter(([id]) => id.length <= 4);
  const long = allOracles.filter(([id]) => id.length === 5);
  process.stderr.write('\nShort: ' + short.length + ', Long: ' + long.length + '\n');
})();
