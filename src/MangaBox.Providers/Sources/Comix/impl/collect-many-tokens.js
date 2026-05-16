// Collect live tokens for many IDs to reverse-engineer the current bitmasks
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const tokens = new Map();

  page.on('request', (req) => {
	const u = req.url();
	const m = u.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?[^#]*[?&]_=([^&]+)/);
	if (m && !tokens.has(m[1])) {
	  tokens.set(m[1], m[2]);
	}
  });

  // Visit main page to collect IDs
  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(8000);

  const ids = await page.$$eval('a[href*="/title/"]', (as) => {
	const uniq = [];
	const set = new Set();
	for (const a of as) {
	  const h = a.getAttribute('href') || '';
	  const m = h.match(/\/title\/([a-z0-9]+)-/i);
	  if (!m) continue;
	  const id = m[1];
	  if (set.has(id)) continue;
	  set.add(id);
	  uniq.push(id);
	  if (uniq.length >= 40) break;
	}
	return uniq;
  });

  console.log('Found IDs:', ids.join(','));

  for (const id of ids) {
	try {
	  await page.goto(`https://comix.to/title/${id}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
	  await page.waitForTimeout(3000);
	} catch {}
  }

  console.log('\nCaptured tokens:');
  for (const [id, tok] of tokens) {
	console.log(`TOK ${id} ${tok}`);
  }

  await browser.close();
})();
