'use strict';
const { chromium } = require('playwright');
const fs = require('fs');

// Target ID to diagnose
const TARGET_ID = '8w6dm';

// Our current signer constants
const PREFIX = [97,200,64,144,162,7,176,70,112,166,46,172,221,0,253,31,196,10,25,32,99,136,29,229,210,13,150,51,132,252,213,72,16,222,85,16,49,197,175,230,100,6,120,233,32,249,167,132,106];
const SUFFIX5 = [127,241,120,206,4,167,103,234,234,27,134];
const MASKS5 = [1n,35n,17973325n,1332340n,20976474n,17189124n,4610622n,32n,256n,544n,1536n,17515604n,6245121n,6249217n,460377n,17826603n,197748n,16976482n,17957399n,21170754n,17192752n,65568n,196608n,21059884n,18092892n,22942580n,0n,18357311n,1595162n,5381143n,6101306n,1660195n,6248992n,18355534n,16977780n,1461359n,6243885n,6119274n,17700693n,18502400n];

function computeSign(id) {
  const masks = MASKS5;
  const suffix = SUFFIX5;
  const varStart = 49, varCount = 5;
  const bytes = [...PREFIX, ...new Array(varCount).fill(0), ...suffix];
  let featureBits = 0n;
  let bitIdx = 0;
  for (let i = 0; i < 5; i++) {
	const ch = BigInt(i < id.length ? id.charCodeAt(i) : 0);
	for (let b = 0; b < 8; b++) {
	  if ((ch >> BigInt(b)) & 1n) featureBits |= 1n << BigInt(bitIdx);
	  bitIdx++;
	}
  }
  for (let ob = 0; ob < masks.length; ob++) {
	let v = featureBits & masks[ob];
	let p = 0n; while (v) { p ^= v & 1n; v >>= 1n; }
	if (p) bytes[varStart + (ob >> 3)] |= (1 << (ob & 7));
  }
  return Buffer.from(bytes).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
}

(async () => {
  const ourToken = computeSign(TARGET_ID);
  console.log('Our token for', TARGET_ID, ':', ourToken);

  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
  const page = await context.newPage();

  // Intercept btoa to capture the signing buffer
  let captured = null;
  await page.addInitScript(() => {
	const origBtoa = window.btoa;
	window.btoa = function(s) {
	  const result = origBtoa.call(this, s);
	  const bytes = Array.from(s).map(c => c.charCodeAt(0));
	  if (bytes.length >= 60) {
		window.__latestSign = { bytes, result };
		console.log('BTOA:', bytes.length, 'bytes, result:', result.slice(0,20));
	  }
	  return result;
	};
  });

  await page.route(`**/${TARGET_ID}/chapters*`, route => {
	console.log('INTERCEPTED:', route.request().url());
	route.continue();
  });

  const tokens = [];
  page.on('request', req => {
	const url = req.url();
	if (url.includes(`/${TARGET_ID}/chapters`) && url.includes('_=')) {
	  const m = url.match(/_=([^&]+)/);
	  if (m) tokens.push(m[1]);
	}
  });

  await page.goto('https://comix.to/', { waitUntil: 'networkidle', timeout: 30000 });
  await page.goto(`https://comix.to/manga/${TARGET_ID}`, { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(5000);

  if (tokens.length === 0) {
	// Try scrolling / clicking to trigger chapter list load
	await page.evaluate(() => window.scrollTo(0, 500));
	await page.waitForTimeout(3000);
  }

  const browserToken = tokens[0] || 'NOT_CAPTURED';
  console.log('Browser token:', browserToken);
  console.log('Our token:    ', ourToken);
  console.log('Match:', browserToken === ourToken);

  if (browserToken !== 'NOT_CAPTURED' && browserToken !== ourToken) {
	// Decode both and compare bytes
	const expBytes = Array.from(Buffer.from(browserToken.replace(/-/g,'+').replace(/_/g,'/') + '=', 'base64'));
	const gotBytes = Array.from(Buffer.from(ourToken.replace(/-/g,'+').replace(/_/g,'/') + '=', 'base64'));
	console.log('\nByte comparison (index: expected vs got):');
	for (let i = 0; i < Math.max(expBytes.length, gotBytes.length); i++) {
	  if (expBytes[i] !== gotBytes[i]) {
		console.log(`  [${i}]: exp=${expBytes[i]} got=${gotBytes[i]} diff_bits=${(expBytes[i]^gotBytes[i]).toString(2).padStart(8,'0')}`);
	  }
	}

	// Get the btoa capture
	const signData = await page.evaluate(() => window.__latestSign);
	if (signData) {
	  console.log('\nRaw btoa bytes:', JSON.stringify(signData.bytes));
	}
  }

  fs.writeFileSync('diag-8w6dm.json', JSON.stringify({ TARGET_ID, ourToken, browserToken, timestamp: new Date().toISOString() }, null, 2));
  await browser.close();
})();
