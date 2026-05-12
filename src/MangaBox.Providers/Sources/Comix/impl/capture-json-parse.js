const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', (msg) => {
	const text = msg.text();
	if (text.startsWith('PARSE_HIT') || text.startsWith('PARSE_ERR')) {
	  console.log(text);
	}
  });

  await page.addInitScript(() => {
	const origParse = JSON.parse;
	JSON.parse = function (text, reviver) {
	  try {
		if (typeof text === 'string' && text.length > 100) {
		  if (text.includes('"status"') && text.includes('"result"') && text.includes('"chapters"')) {
			console.log('PARSE_HIT ' + text.slice(0, 500));
		  }
		}
	  } catch (e) {
		console.log('PARSE_ERR ' + (e?.message || e));
	  }
	  return origParse.call(this, text, reviver);
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(12000);
  await browser.close();
})();
