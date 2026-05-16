'use strict';
// Intercept and dump the actual signer computation from browser via CDP
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false, args: ['--enable-logging'] });
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  const client = await ctx.newCDPSession(page);

  // Enable network and debugger
  await client.send('Debugger.enable');
  await client.send('Network.enable');

  const signFnSource = [];

  // Watch for requests to chapters endpoint and trace back
  page.on('request', req => {
	const url = req.url();
	if (url.includes('/chapters?') && url.includes('_=')) {
	  const m = url.match(/manga\/([^/]+)\/chapters.*_=([\w\-]+)/);
	  if (m) process.stderr.write('REQUEST: id=' + m[1] + ' token=' + m[2] + '\n');
	}
  });

  // Inject instrumentation before page JS runs
  await page.addInitScript(() => {
	// Intercept XMLHttpRequest and fetch to see what parameters generate the token
	const orig = window.fetch;
	window.fetch = function(...args) {
	  const url = typeof args[0] === 'string' ? args[0] : args[0]?.url;
	  if (url && url.includes('chapters') && url.includes('_=')) {
		console.log('FETCH_URL: ' + url);
	  }
	  return orig.apply(this, args);
	};
  });

  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  // Navigate to a title to trigger signing
  await page.goto('https://comix.to/title/55kym', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForTimeout(3000);

  // Try to find the signing function in page context
  const result = await page.evaluate(() => {
	// Look for function that generates the _ parameter
	// Try to search through window properties for anything sign-related
	const keys = Object.keys(window).filter(k => /sign|token|hash|manga/i.test(k));
	return keys;
  });
  process.stderr.write('Window keys with sign/token/hash/manga: ' + JSON.stringify(result) + '\n');

  // Search JS files for signing code
  const resources = await page.evaluate(() => {
	return performance.getEntriesByType('resource')
	  .filter(r => r.initiatorType === 'script')
	  .map(r => r.name);
  });
  process.stderr.write('Script resources: ' + JSON.stringify(resources.slice(0, 20)) + '\n');

  await browser.close();
})();
