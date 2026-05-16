const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  let count = 0;
  page.on('request', req => {
	const url = req.url();
	if (url.includes('api/v1/manga')) { count++; process.stderr.write('REQ: ' + url.substring(0, 120) + '\n'); }
  });
  try {
	await page.goto('https://comix.to/en/title/55kwg', { waitUntil: 'domcontentloaded', timeout: 30000 });
	await page.waitForTimeout(5000);
  } catch(e) { process.stderr.write('nav error: ' + e.message + '\n'); }
  process.stderr.write('Total api requests: ' + count + '\n');
  await browser.close();
})();
