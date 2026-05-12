const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(5000);

  const out = await page.evaluate(() => {
	const names = ['Qi', 'Qx', 'Qd', 'QC'];
	const data = {};
	for (const n of names) {
	  const v = window[n];
	  data[n] = {
		type: typeof v,
		len: typeof v === 'function' ? v.length : null,
		src: typeof v === 'function' ? v.toString().slice(0, 4000) : null,
	  };
	}
	return data;
  });

  console.log(JSON.stringify(out));
  await browser.close();
})();
