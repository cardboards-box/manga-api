const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', (msg) => {
	const text = msg.text();
	if (text.startsWith('ATOB_CALL')) console.log(text);
  });

  await page.addInitScript(() => {
	const origAtob = window.atob.bind(window);
	const seen = new Set();
	window.atob = function (s) {
	  try {
		const out = origAtob(s);
		if (!seen.has(s)) {
		  seen.add(s);
		  console.log('ATOB_CALL ' + JSON.stringify({ in: s, inLen: s.length, outLen: out.length }));
		}
		return out;
	  } catch {
		return origAtob(s);
	  }
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(12000);
  await browser.close();
})();
