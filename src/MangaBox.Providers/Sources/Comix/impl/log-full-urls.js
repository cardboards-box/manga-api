'use strict';
// Capture full chapter-list URLs to see if query params vary
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('request', req => {
	const url = req.url();
	if (url.includes('/api/v1/manga/') && url.includes('/chapters')) {
	  process.stderr.write('URL: ' + url + '\n');
	}
  });

  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  // Navigate to a couple title pages
  for (const id of ['55kwg', 'n93ny', 'gxgm9']) {
	await page.goto('https://comix.to/title/' + id, { waitUntil: 'domcontentloaded', timeout: 30000 });
	await page.waitForTimeout(3000);
  }

  await browser.close();
})();
