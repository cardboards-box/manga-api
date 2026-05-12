const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', (msg) => {
	const text = msg.text();
	if (text.startsWith('DECRYPTED_JSON') || text.startsWith('FETCH_E_LEN')) {
	  console.log(text);
	}
  });

  await page.addInitScript(() => {
	const origDecode = TextDecoder.prototype.decode;
	TextDecoder.prototype.decode = function (...args) {
	  const out = origDecode.apply(this, args);
	  if (typeof out === 'string' && out.length > 100 && out[0] === '{' && out.includes('"status"') && out.includes('"result"')) {
		console.log('DECRYPTED_JSON ' + out.slice(0, 500));
	  }
	  return out;
	};

	const origFetch = window.fetch.bind(window);
	window.fetch = async (...args) => {
	  const response = await origFetch(...args);
	  try {
		const url = typeof args[0] === 'string' ? args[0] : args[0]?.url;
		if (url && url.includes('/api/v1/manga/') && url.includes('/chapters')) {
		  const cloned = response.clone();
		  const text = await cloned.text();
		  if (text.startsWith('{"e":"')) {
			console.log('FETCH_E_LEN ' + text.length);
		  }
		}
	  } catch {
	  }
	  return response;
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(12000);
  await browser.close();
})();
