'use strict';
// Deep runtime hook - intercept XHR.open to capture the exact signing call chain
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  const client = await ctx.newCDPSession(page);

  await client.send('Debugger.enable');
  await client.send('Runtime.enable');

  const allTokens = [];
  let signing = false;

  // Intercept at a low level - override XMLHttpRequest.open before any page JS runs
  await page.addInitScript(() => {
	window.__tokens = [];

	// Hook into the low-level XHR
	const origOpen = XMLHttpRequest.prototype.open;
	XMLHttpRequest.prototype.open = function(method, url) {
	  if (typeof url === 'string' && url.includes('/chapters?') && url.includes('_=')) {
		const m = url.match(/manga\/([^/]+)\/chapters.*[?&]_=([\w-]+)/);
		if (m) {
		  // Capture the full stack trace
		  const stack = new Error('CAPTURED').stack;
		  window.__tokens.push({ id: m[1], token: m[2], stack });
		}
	  }
	  return origOpen.apply(this, arguments);
	};

	// Also hook Uint8Array constructor to catch the signing moment
	const origUint8 = Uint8Array;
	let callCount = 0;
	window.Uint8Array = function(...args) {
	  callCount++;
	  const inst = new origUint8(...args);
	  return inst;
	};
	window.Uint8Array.prototype = origUint8.prototype;
	Object.setPrototypeOf(window.Uint8Array, origUint8);

	// Hook btoa / atob
	const origBtoa = window.btoa;
	window.btoa = function(data) {
	  const result = origBtoa.call(window, data);
	  if (result.length > 80) {
		window.__tokens.push({ btoa: result.slice(0, 100), stack: new Error().stack.split('\n').slice(1,4).join('|') });
	  }
	  return result;
	};
  });

  await page.goto('https://comix.to/title/55kym', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  const data = await page.evaluate(() => window.__tokens);
  console.log(JSON.stringify(data, null, 2));

  // Now try to find and call the signer directly
  // Navigate to a page and extract the signing function via the module system
  const signerFn = await page.evaluate(async (mangaId) => {
	try {
	  // Look for nuxt/vite module registry
	  let moduleData = null;

	  // Try __vite_module_map__ or similar
	  for (const key of ['__vite__', '__import__', '__modules__']) {
		if (window[key]) { moduleData = { key, type: typeof window[key] }; break; }
	  }

	  // Try to find the signing function in the event handlers / composables
	  // by calling a known signed endpoint manually
	  const XHR = window.XMLHttpRequest;
	  return moduleData;
	} catch(e) { return { error: e.message }; }
  }, '55kym');
  console.log('Signer fn:', JSON.stringify(signerFn));

  await browser.close();
})();
