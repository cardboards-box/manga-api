// Capture chapter detail request URL
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('request', (req) => {
	const u = req.url();
	if (u.includes('/api/v1/chapters/')) {
	  console.log('CHAPTER-DETAIL:', u);
	}
  });

  // Visit manga page to get chapter links
  await page.goto('https://comix.to/title/55kwg-moto-yuusha-wa-monster-musume-ni-hairaretai', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  // Click first chapter link
  const hrefs = await page.$$eval('a[href*="/chapter/"]', els => els.map(e => e.href));
  console.log('Chapter links:', hrefs.slice(0,3));

  if (hrefs.length > 0) {
	await page.goto(hrefs[0], { waitUntil: 'domcontentloaded', timeout: 60000 });
	await page.waitForTimeout(5000);
  }

  await browser.close();
})();
