'use strict';
// Step 1: Check what the Comix homepage actually renders and what API calls are made
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const page = await browser.newPage();

  const requests = [];
  page.on('request', req => {
	const url = req.url();
	if (url.includes('comix.to/api') || url.includes('comix.to/manga')) {
	  requests.push(url);
	}
  });

  await page.goto('https://comix.to/', { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(3000);

  // Try different selectors
  const checks = await page.evaluate(() => ({
	allLinks: Array.from(document.querySelectorAll('a[href]')).slice(0, 20).map(a => a.href),
	mangaLinks: Array.from(document.querySelectorAll('a[href*="manga"]')).slice(0, 10).map(a => a.href),
	bodyText: document.body.innerHTML.slice(0, 500),
	title: document.title,
  }));

  console.log('Title:', checks.title);
  console.log('Sample links:', JSON.stringify(checks.allLinks.slice(0, 10), null, 2));
  console.log('Manga links:', JSON.stringify(checks.mangaLinks, null, 2));
  console.log('API requests:', JSON.stringify(requests.slice(0, 20), null, 2));
  console.log('Body snippet:', checks.bodyText.slice(0, 300));

  await browser.close();
})();
