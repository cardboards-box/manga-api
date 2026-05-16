'use strict';
// Intercept the token computation at runtime via Playwright CDP
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const ctx = await browser.newContext();
  const page = await ctx.newPage();

  const captured = [];

  // Intercept fetch requests and log the _ param
  await page.addInitScript(() => {
	const origFetch = window.fetch;
	window._capturedTokens = [];
	window.fetch = function(...args) {
	  const url = typeof args[0] === 'string' ? args[0] : (args[0]?.url ?? '');
	  if (url && url.includes('/chapters?') && url.includes('_=')) {
		const m = url.match(/manga\/([^/]+)\/chapters.*[?&]_=([\w\-]+)/);
		if (m) window._capturedTokens.push({ id: m[1], token: m[2], url });
	  }
	  return origFetch.apply(this, args);
	};

	// Also intercept XMLHttpRequest
	const origOpen = XMLHttpRequest.prototype.open;
	XMLHttpRequest.prototype.open = function(method, url) {
	  if (url && url.includes('/chapters?') && url.includes('_=')) {
		const m = url.match(/manga\/([^/]+)\/chapters.*[?&]_=([\w\-]+)/);
		if (m) window._capturedTokens.push({ id: m[1], token: m[2], url, via: 'xhr' });
	  }
	  return origOpen.apply(this, arguments);
	};
  });

  await page.goto('https://comix.to/title/55kym', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  const tokens = await page.evaluate(() => window._capturedTokens);
  console.log('Captured tokens:', JSON.stringify(tokens, null, 2));

  // Now try to find the signing function via CDP evaluation
  // Look for any function that takes a manga ID and returns a base64url token
  const result = await page.evaluate(() => {
	// Try to find the function that generates _ by searching webpack modules
	const keys = Object.keys(window);
	return keys.filter(k => {
	  try { return typeof window[k] === 'function' && window[k].toString().includes('chapters'); } catch { return false; }
	});
  });
  console.log('Functions with chapters:', result);

  // Try webpack module introspection
  const modules = await page.evaluate(() => {
	try {
	  // Common webpack chunk globals
	  for (const key of Object.keys(window)) {
		if (key.startsWith('webpackChunk') || key.includes('webpack')) return key;
	  }
	  return 'none';
	} catch(e) { return 'error: ' + e.message; }
  });
  console.log('Webpack global:', modules);

  await browser.close();
})();
