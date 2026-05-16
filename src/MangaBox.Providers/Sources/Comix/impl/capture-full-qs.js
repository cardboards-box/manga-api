// Capture full query string of live chapter-list and chapter-detail requests
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('request', (req) => {
	const u = req.url();
	if (u.includes('/api/v1/manga/') && u.includes('/chapters')) {
	  console.log('CHAPTER-LIST:', u);
	}
	if (u.includes('/api/v1/chapters/')) {
	  console.log('CHAPTER-DETAIL:', u);
	}
  });

  // Visit a manga title page first to get chapter list request
  await page.goto('https://comix.to/title/55kwg-moto-yuusha-wa-monster-musume-ni-hairaretai', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  // Now click a chapter to trigger chapter-detail request
  try {
	const chapterLinks = await page.$$('a[href*="/chapter/"]');
	if (chapterLinks.length > 0) {
	  await chapterLinks[0].click();
	  await page.waitForTimeout(5000);
	}
  } catch(e) {
	console.log('Click error:', e.message);
  }

  await browser.close();
})();
