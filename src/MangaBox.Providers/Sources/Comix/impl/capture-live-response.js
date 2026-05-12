const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('response', async (response) => {
	const url = response.url();
	if (!url.includes('/api/v1/manga/8w6dm/chapters')) return;

	try {
	  const status = response.status();
	  const body = await response.text();
	  console.log('STATUS', status);
	  console.log('LEN', body.length);
	  console.log('HEAD', body.slice(0, 300));
	} catch (e) {
	  console.error('READ_ERROR', e?.message || e);
	}
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(8000);
  await browser.close();
})();
