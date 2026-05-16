'use strict';
// Hook into the actual signing function by intercepting XHR/fetch params construction
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  const client = await ctx.newCDPSession(page);
  await client.send('Debugger.enable');

  const results = [];

  // Listen for console messages from the page
  page.on('console', msg => {
	if (msg.text().startsWith('SIGN:')) {
	  results.push(msg.text());
	  process.stderr.write(msg.text() + '\n');
	}
  });

  // Add init script to intercept all property/param setters
  await page.addInitScript(() => {
	// Override URLSearchParams to capture _ param
	const OrigURLSearchParams = window.URLSearchParams;
	window.URLSearchParams = function(...args) {
	  const inst = new OrigURLSearchParams(...args);
	  const origSet = inst.set.bind(inst);
	  const origAppend = inst.append.bind(inst);
	  inst.set = function(k, v) {
		if (k === '_') console.log('SIGN: URLSearchParams.set _ = ' + v + ' stack=' + new Error().stack.split('\n').slice(1,4).join('|'));
		return origSet(k, v);
	  };
	  inst.append = function(k, v) {
		if (k === '_') console.log('SIGN: URLSearchParams.append _ = ' + v + ' stack=' + new Error().stack.split('\n').slice(1,4).join('|'));
		return origAppend(k, v);
	  };
	  return inst;
	};
	Object.setPrototypeOf(window.URLSearchParams, OrigURLSearchParams);

	// Also intercept fetch to see the full URL
	const origFetch = window.fetch;
	window.fetch = async function(input, init) {
	  const url = typeof input === 'string' ? input : input?.url;
	  if (url && url.includes('chapters') && url.includes('_=')) {
		const m = url.match(/manga\/([^/]+)\/chapters[^?]*\?(.+)/);
		if (m) console.log('SIGN: fetch manga=' + m[1] + ' params=' + m[2].slice(0,100));
	  }
	  return origFetch.apply(this, arguments);
	};
  });

  await page.goto('https://comix.to/title/55kym', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(6000);

  // Try to extract the signing function via webpack module system
  const webpackInfo = await page.evaluate(() => {
	try {
	  // Find webpack require
	  for (const key of Object.keys(window)) {
		if (key.includes('webpack') || key.startsWith('__webpack')) {
		  return { key, type: typeof window[key] };
		}
	  }
	  // Try React internal fiber
	  const el = document.querySelector('#__nuxt') || document.querySelector('#app') || document.querySelector('[data-v-app]');
	  return { el: el?.tagName, keys: Object.keys(window).filter(k => k.startsWith('__')).slice(0, 20) };
	} catch(e) { return { error: e.message }; }
  });
  process.stderr.write('Webpack info: ' + JSON.stringify(webpackInfo) + '\n');

  // Try to call the Nuxt app module system
  const signerResult = await page.evaluate(async () => {
	try {
	  // Nuxt 3 / Vue 3 apps expose their internals differently
	  // Try to find the composable / API function
	  const app = window.__nuxt_app__ || window.__vue_app__;
	  if (app) {
		return { type: 'nuxt', keys: Object.keys(app).slice(0, 20) };
	  }
	  return { type: 'none' };
	} catch(e) { return { error: e.message }; }
  });
  process.stderr.write('Signer result: ' + JSON.stringify(signerResult) + '\n');

  await page.waitForTimeout(2000);
  process.stderr.write('\nAll results: ' + JSON.stringify(results) + '\n');

  await browser.close();
})();
