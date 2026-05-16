// Collect tokens for more 5-char IDs to improve coverage  
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const tokens = new Map();

  page.on('request', (req) => {
	const u = req.url();
	const m = u.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?.*[?&]_=([^&]+)/);
	if (m && !tokens.has(m[1])) tokens.set(m[1], m[2]);
  });

  // Known IDs we need
  const targetIds = ['60jxz', '8w6dm', 'aaaaa', 'bbbbb', 'ccccc', 'ddddd', 'eeeee'];

  for (const id of targetIds) {
	try {
	  await page.goto(`https://comix.to/title/${id}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
	  await page.waitForTimeout(3000);
	} catch {}
  }

  // Also scrape from main page
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
	  if (set.has(id) || id.length <= 4) continue;  // only 5-char IDs
	  set.add(id);
	  uniq.push(id);
	  if (uniq.length >= 50) break;
	}
	return uniq;
  });

  for (const id of ids) {
	if (tokens.has(id)) continue;
	try {
	  await page.goto(`https://comix.to/title/${id}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
	  await page.waitForTimeout(3000);
	} catch {}
  }

  console.log(`Collected ${tokens.size} tokens:`);
  for (const [id, tok] of tokens) {
	console.log(`['${id}', '${tok}'],`);
  }

  await browser.close();
})();
