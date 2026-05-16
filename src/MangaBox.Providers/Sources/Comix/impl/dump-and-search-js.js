'use strict';
// Download and search the app JS chunks for the token signer
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  const scripts = new Map();
  page.on('response', async resp => {
	const url = resp.url();
	if ((url.includes('.js') || url.includes('_nuxt')) && 
		!url.includes('google') && !url.includes('gtag') && 
		!url.includes('facebook') && !url.includes('analytics')) {
	  try {
		const ct = resp.headers()['content-type'] || '';
		if (ct.includes('javascript') || url.match(/\.(js|mjs)(\?|$)/)) {
		  const text = await resp.text();
		  scripts.set(url, text);
		}
	  } catch {}
	}
  });

  await page.goto('https://comix.to', { waitUntil: 'networkidle', timeout: 90000 });
  await page.waitForTimeout(2000);

  await browser.close();
  process.stderr.write('Downloaded ' + scripts.size + ' script files\n');

  const outDir = path.join(__dirname, 'js-dump');
  fs.mkdirSync(outDir, { recursive: true });

  for (const [url, text] of scripts) {
	const fname = url.replace(/[^a-z0-9]/gi, '_').slice(-60) + '.js';
	const fpath = path.join(outDir, fname);
	fs.writeFileSync(fpath, text);

	// Search for signing-related code
	if (/charCodeAt|btoa|atob|base64|ArrayBuffer|Uint8Array/.test(text) &&
		/chapters|manga.*id|sign|token/i.test(text)) {
	  process.stderr.write('\nCANDIDATE: ' + url + ' (' + text.length + ' bytes) -> ' + fname + '\n');

	  // Find and print relevant snippets
	  const lines = text.split(/[;\n]/);
	  for (let i = 0; i < lines.length; i++) {
		const l = lines[i];
		if (/charCodeAt.*manga|manga.*charCodeAt|chapters.*_=|_=.*chapters|btoa.*manga|sign.*manga/i.test(l)) {
		  process.stderr.write('  MATCH[' + i + ']: ' + l.slice(0, 200) + '\n');
		}
	  }
	}
  }

  // Find the file most likely to contain the signer
  let best = null, bestScore = 0;
  for (const [url, text] of scripts) {
	let score = 0;
	if (/charCodeAt/.test(text)) score += 10;
	if (/Uint8Array|ArrayBuffer/.test(text)) score += 5;
	if (/chapters/.test(text)) score += 10;
	if (/manga.*id|id.*manga/i.test(text)) score += 5;
	if (/atob|btoa|base64/i.test(text)) score += 3;
	if (score > bestScore) { bestScore = score; best = url; }
  }
  process.stderr.write('\nBest candidate: ' + best + ' (score=' + bestScore + ')\n');

  // Search ALL files for the _= pattern near manga/chapters
  for (const [url, text] of scripts) {
	const idx = text.indexOf('_=');
	if (idx >= 0) {
	  const snippet = text.slice(Math.max(0, idx-100), idx+200);
	  if (/manga|chapter|sign/i.test(snippet)) {
		process.stderr.write('\nFOUND _= in: ' + url + '\n' + snippet + '\n');
	  }
	}
  }
})();
