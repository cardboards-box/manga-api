// Use browser runtime to extract current signing constants by calling the signer directly
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const page = await browser.newPage();

  const tokens = {};

  page.on('request', (req) => {
	const u = req.url();
	const m = u.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?.*[?&]_=([^&]+)/);
	if (m) tokens[m[1]] = m[2];
	const m2 = u.match(/\/api\/v1\/chapters\/([^?]+)\?.*[?&]_=([^&]+)/);
	if (m2) tokens['chapter:'+m2[1]] = m2[2];
  });

  await page.goto('https://comix.to/title/55kwg-moto-yuusha-wa-monster-musume-ni-hairaretai', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  // Extract the signing function from the window context
  const result = await page.evaluate(async () => {
	// Try to find the signer by hooking fetch
	const originalFetch = window.fetch;
	const calls = [];

	window.fetch = function(url, opts) {
	  calls.push({ url: typeof url === 'string' ? url : url?.toString(), time: Date.now() });
	  return originalFetch.apply(this, arguments);
	};

	// Wait a bit for any in-flight requests
	await new Promise(r => setTimeout(r, 1000));

	return {
	  calls: calls.slice(0, 10),
	  // Try to find keys in JS heap via globalThis
	  keys: Object.keys(globalThis).filter(k => k.length < 10).slice(0, 50)
	};
  });

  console.log('Fetch calls:', JSON.stringify(result.calls.slice(0,5)));
  console.log('');

  // Now try to get the token for specific test IDs via programmatic navigation
  const testIds = ['55kwg', 'aaaaa', 'bbbbb', '60jxz'];
  for (const id of testIds) {
	// Use fetch directly from the browser context - the page's intercepted fetch will sign the request
	const token = await page.evaluate(async (mangaId) => {
	  return new Promise((resolve) => {
		const orig = window.fetch;
		const hookedFetch = function(url, opts) {
		  const u = typeof url === 'string' ? url : url?.toString?.() || '';
		  if (u.includes(`/manga/${mangaId}/chapters`)) {
			const m = u.match(/[?&]_=([^&]+)/);
			if (m) { resolve(m[1]); return orig.apply(this, arguments); }
		  }
		  return orig.apply(this, arguments);
		};
		window.fetch = hookedFetch;

		// Trigger a fetch by navigating (or using the app's API client if accessible)
		// Try direct API call
		fetch(`/api/v1/manga/${mangaId}/chapters?page=1&limit=20&order[number]=desc`).catch(() => {});

		setTimeout(() => resolve(null), 3000);
	  });
	}, id);

	if (token) {
	  console.log(`TOKEN ${id}: ${token}`);
	} else {
	  // Check from captured requests
	  if (tokens[id]) console.log(`TOKEN ${id}: ${tokens[id]} (from intercept)`);
	  else console.log(`TOKEN ${id}: not captured`);
	}
  }

  console.log('\nAll captured tokens:');
  for (const [id, tok] of Object.entries(tokens)) {
	console.log(`  ${id}: ${tok}`);
  }

  await browser.close();
})();
