// Collect token for 60jxz specifically
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('request', (req) => {
	const u = req.url();
	const m = u.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?.*[?&]_=([^&]+)/);
	if (m) console.log(`TOK ${m[1]} ${m[2]}`);
  });

  await page.goto('https://comix.to/title/60jxz', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  await browser.close();
})();
