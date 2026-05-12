const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const tokens = new Map();

  page.on('request', (req) => {
	const u = req.url();
	const m = u.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?[^#]*[?&]_=(.+)$/);
	if (m && !tokens.has(m[1])) {
	  tokens.set(m[1], m[2]);
	  console.log(`TOK ${m[1]} ${m[2]}`);
	}
  });

  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 120000 });
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
	  if (uniq.length >= 20) break;
	}
	return uniq;
  });

  console.log(`IDS ${ids.join(',')}`);

  for (const id of ids) {
	try {
	  await page.goto(`https://comix.to/title/${id}`, { waitUntil: 'domcontentloaded', timeout: 120000 });
	  await page.waitForTimeout(5000);
	} catch {
	  console.log(`ERR ${id}`);
	}
  }

  console.log(`COUNT ${tokens.size}`);
  await browser.close();
})();
