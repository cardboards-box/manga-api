// Capture what the browser sends as _= token for chapter 8882132
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('request', (req) => {
	const u = req.url();
	if (u.includes('/api/v1/chapters/') || u.includes('/api/v1/manga/')) {
	  console.log('API:', u);
	}
  });

  await page.goto('https://comix.to/chapter/8882132', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  await browser.close();
})();
