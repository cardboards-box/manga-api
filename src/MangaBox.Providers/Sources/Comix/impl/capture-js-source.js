'use strict';
// Capture the actual signing JavaScript from comix.to browser
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  // Intercept all JS files to find the signer
  const jsFiles = [];
  page.on('response', async resp => {
	const url = resp.url();
	const ct = resp.headers()['content-type'] || '';
	if (ct.includes('javascript') && !url.includes('google') && !url.includes('facebook')) {
	  try {
		const text = await resp.text();
		// Look for signing-related patterns
		if (text.includes('_=') || text.includes('chapters') || text.includes('sign') || text.length < 500000) {
		  jsFiles.push({ url, text, size: text.length });
		}
	  } catch {}
	}
  });

  await page.goto('https://comix.to', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(3000);

  // Sort by likely relevance (smaller files first, avoid huge bundles)
  jsFiles.sort((a, b) => a.size - b.size);

  for (const { url, text, size } of jsFiles.slice(0, 20)) {
	process.stderr.write('\n=== JS FILE: ' + url + ' (' + size + ' bytes) ===\n');

	// Search for signing-related patterns
	const patterns = [
	  /function[^{]*sign[^{]*/gi,
	  /['"_='"]/g,
	  /chapters.*_=/gi,
	  /\.charCodeAt/g,
	  /manga.*id/gi,
	];

	// Look for the relevant sections
	const lines = text.split('\n');
	for (let i = 0; i < lines.length; i++) {
	  const line = lines[i];
	  if (/charCodeAt|sign|chapters.*_=|_=.*sign|token.*gen|generat.*token/i.test(line)) {
		const start = Math.max(0, i - 2);
		const end = Math.min(lines.length, i + 5);
		process.stderr.write('  LINE ' + i + ': ' + lines.slice(start, end).join('\n  ') + '\n');
	  }
	}
  }

  await browser.close();
})();
