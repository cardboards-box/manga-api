const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', msg => {
	const t = msg.text();
	if (t.startsWith('STACK_')) console.log(t);
  });

  await page.addInitScript(() => {
	const original = String.fromCharCode.apply.bind(String.fromCharCode);
	let dumped = false;

	String.fromCharCode.apply = function (thisArg, arr) {
	  if (!dumped && arr && typeof arr.length === 'number' && arr.length === 8966) {
		dumped = true;
		try {
		  const stack = new Error('trace').stack || '';
		  console.log('STACK_CALL ' + JSON.stringify({ stack }));
		} catch {
		}
	  }
	  return original(thisArg, arr);
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(10000);
  await browser.close();
})();
