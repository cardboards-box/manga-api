// Capture the current live signing constants from comix.to's JS bundle
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  // Intercept the main JS bundle
  const bundles = [];
  page.on('response', async (resp) => {
	const url = resp.url();
	if (url.includes('/_next/static/chunks/') && url.endsWith('.js')) {
	  try {
		const text = await resp.text();
		// Look for the signing-related constants (large array of numbers that look like bit masks)
		// The prefix array starts with [97,200,64,...]
		if (text.includes('97,200,64,144') || text.includes('105991738') || text.includes('51780415')) {
		  bundles.push({ url, text });
		  console.log('FOUND signing bundle:', url, 'size:', text.length);
		}
	  } catch(e) {}
	}
  });

  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(8000);

  for (const b of bundles) {
	// Extract the relevant section
	const idx97 = b.text.indexOf('97,200,64,144');
	if (idx97 >= 0) {
	  console.log('\n--- PREFIX ARRAY CONTEXT ---');
	  console.log(b.text.substring(Math.max(0, idx97 - 50), idx97 + 300));
	}
	const idx105 = b.text.indexOf('105991738');
	if (idx105 >= 0) {
	  console.log('\n--- BITMASKS CONTEXT ---');
	  console.log(b.text.substring(Math.max(0, idx105 - 100), idx105 + 2000));
	}
  }

  if (bundles.length === 0) {
	console.log('No signing bundle found - trying with navigation to title page');
  }

  await browser.close();
})();
