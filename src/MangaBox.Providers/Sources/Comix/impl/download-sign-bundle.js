// Download current comix.to bundle and extract signing constants
const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  const bundleData = [];

  // Intercept all JS responses
  page.on('response', async (resp) => {
	const url = resp.url();
	if (!url.endsWith('.js')) return;
	try {
	  const text = await resp.text();
	  // The prefix array [97,200,64,144,...] is the key marker
	  // Also check for the bitmask values
	  const hasSigning = text.includes('97,200,64,144') || 
						  text.includes('105991738') || 
						  text.includes('51780415') ||
						  // New constants we might find
						  text.includes('97,200,64');
	  if (hasSigning) {
		bundleData.push({ url, text });
		console.log('FOUND:', url, 'length:', text.length);
		fs.writeFileSync('current-sign-bundle.js', text);
	  }
	} catch(e) {}
  });

  await page.goto('https://comix.to/title/55kwg-moto-yuusha-wa-monster-musume-ni-hairaretai', { waitUntil: 'networkidle', timeout: 90000 });
  await page.waitForTimeout(5000);

  if (bundleData.length === 0) {
	// Try intercepting all scripts
	const scripts = await page.$$eval('script[src]', els => els.map(e => e.src));
	console.log('Scripts found:', scripts.length);
	for (const src of scripts.slice(0, 10)) console.log(' ', src);
  }

  await browser.close();
})();
