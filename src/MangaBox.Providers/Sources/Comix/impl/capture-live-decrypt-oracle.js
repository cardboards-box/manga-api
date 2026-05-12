const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.addInitScript(() => {
	window.__decryptStates = [];

	const orig = String.fromCharCode.apply.bind(String.fromCharCode);
	String.fromCharCode.apply = function (thisArg, arr) {
	  try {
		if (arr && typeof arr.length === 'number' && arr.length >= 8500 && arr.length <= 9200) {
		  const snapshot = Array.from(arr);
		  window.__decryptStates.push(snapshot);
		}
	  } catch {
	  }
	  return orig(thisArg, arr);
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(12000);

  const states = await page.evaluate(() => window.__decryptStates || []);
  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/live-decrypt-oracle.json', JSON.stringify(states));
  console.log('captured states', states.length, states.map(s => s.length));

  await browser.close();
})();
