'use strict';
// Extract the signing function's pure logic by logging all intermediate values at runtime
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const ctx = await browser.newContext();
  const page = await ctx.newPage();

  // Override crypto and other APIs to trace what data is fed into btoa
  await page.addInitScript(() => {
	window.__signingData = [];

	// Track all Uint8Array operations near the signing moment
	const origBtoa = window.btoa;
	window.btoa = function(data) {
	  const result = origBtoa.call(window, data);
	  if (result.length > 80) {
		// This is the signing btoa! Capture the input bytes
		const bytes = [...data].map(c => c.charCodeAt(0));
		window.__signingData.push({ 
		  inputBytes: bytes,
		  output: result,
		  len: bytes.length
		});
		// Also log a stack trace
		console.log('BTOA_SIGNING: input_len=' + bytes.length + ' bytes_49_54=' + JSON.stringify(bytes.slice(49, 54)));
	  }
	  return result;
	};
  });

  await page.on('console', msg => {
	if (msg.text().startsWith('BTOA_SIGNING:')) {
	  process.stderr.write(msg.text() + '\n');
	}
  });

  await page.goto('https://comix.to/title/55kym', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(4000);

  const data = await page.evaluate(() => window.__signingData);

  if (data.length > 0) {
	console.log('\n=== Signing data captured ===');
	for (const d of data) {
	  console.log('Input length:', d.len);
	  console.log('Input bytes (all):', d.inputBytes.join(','));
	  console.log('Output (b64):', d.output.slice(0, 100));
	}
  } else {
	console.log('No signing data captured');
  }

  // Navigate to another page to capture a different ID
  await page.goto('https://comix.to/title/n93ny', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForTimeout(4000);

  const data2 = await page.evaluate(() => window.__signingData);
  console.log('\n=== Second page signing data ===');
  for (const d of data2) {
	console.log('Input bytes:', d.inputBytes.join(','));
	console.log('Output:', d.output.slice(0, 100));
  }

  await browser.close();
})();
