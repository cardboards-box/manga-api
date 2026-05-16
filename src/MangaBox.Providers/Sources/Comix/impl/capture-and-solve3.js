'use strict';
// Capture fresh tokens in a SINGLE browser session and solve immediately.
// Handles short IDs (<=4 chars, 32-bit variable region) and long IDs (5 chars, 40-bit) separately.
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const tokens = new Map(); // id -> token string

  page.on('request', req => {
	const url = req.url();
	const m = url.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?[^#]*[?&]_=([\w\-]+)/);
	if (m && !tokens.has(m[1])) {
	  tokens.set(m[1], m[2]);
	  process.stderr.write('captured [' + tokens.size + '] ' + m[1] + ' (' + m[1].length + '-char) ' + m[2].substring(0, 20) + '...\n');
	}
  });

  // Load homepage to get IDs
  process.stderr.write('Loading homepage...\n');
  await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const ids = await page.$$eval('a[href*="/title/"]', (as) => {
	const uniq = [], seen = new Set();
	for (const a of as) {
	  const h = a.getAttribute('href') || '';
	  const m = h.match(/\/title\/([a-z0-9]+)/i);
	  if (!m) continue;
	  const id = m[1];
	  if (seen.has(id)) continue;
	  seen.add(id);
	  uniq.push(id);
	  if (uniq.length >= 60) break;
	}
	return uniq;
  });
  process.stderr.write('Found IDs: ' + ids.length + '\n');

  for (const id of ids) {
	if (tokens.size >= 55) break;
	try {
	  await page.goto('https://comix.to/title/' + id, { waitUntil: 'domcontentloaded', timeout: 30000 });
	  await page.waitForTimeout(3000);
	} catch (e) {
	  process.stderr.write('nav error ' + id + ': ' + e.message + '\n');
	}
  }

  await browser.close();
  process.stderr.write('Total captured: ' + tokens.size + '\n');

  // --- Solver helpers ---
  function fromB64url(s) {
	return [...Buffer.from(s.replace(/-/g, '+').replace(/_/g, '/'), 'base64')];
  }
  function buildFB(id, n) {
	const bits = [];
	for (let i = 0; i < n; i++) {
	  const ch = i < id.length ? id.charCodeAt(i) : 0;
	  for (let b = 0; b < 8; b++) bits.push((ch >> b) & 1);
	}
	return bits;
  }
  function maskBitsToNum(bits) {
	let n = 0n;
	for (let i = 0; i < bits.length; i++) if (bits[i]) n |= 1n << BigInt(i);
	return n;
  }
  function solvGF2(A, b_col) {
	const m = A.length, n = A[0].length;
	const mat = A.map((row, i) => [...row, b_col[i]]);
	const pc = []; let row = 0;
	for (let col = 0; col < n && row < m; col++) {
	  let pr = -1;
	  for (let r = row; r < m; r++) if (mat[r][col] === 1) { pr = r; break; }
	  if (pr < 0) continue;
	  [mat[row], mat[pr]] = [mat[pr], mat[row]];
	  pc.push({ row, col });
	  for (let r = 0; r < m; r++) {
		if (r !== row && mat[r][col] === 1)
		  for (let c = col; c <= n; c++) mat[r][c] ^= mat[row][c];
	  }
	  row++;
	}
	for (let r = row; r < m; r++) if (mat[r][n] === 1) return null;
	const x = new Array(n).fill(0);
	for (const { row: r, col: c } of pc) x[c] = mat[r][n];
	return x;
  }

  function solveGroup(name, oracles, charCount, varBits) {
	if (oracles.length < varBits) {
	  process.stderr.write(name + ': only ' + oracles.length + ' tokens, need >= ' + varBits + ', skipping\n');
	  return null;
	}

	const allB = oracles.map(([, tok]) => fromB64url(tok));
	const pfx = allB[0].slice(0, 49);
	const sfxStart = 49 + (varBits / 8);
	const sfx = allB[0].slice(sfxStart);

	// Verify consistent prefix/suffix across all tokens
	for (const b of allB) {
	  for (let i = 0; i < 49; i++) {
		if (b[i] !== pfx[i]) {
		  process.stderr.write(name + ': MIXED SESSIONS - prefix mismatch!\n');
		  return null;
		}
	  }
	}

	const A = oracles.map(([id]) => buildFB(id, charCount));
	const masks = [];
	let inc = 0;
	for (let ob = 0; ob < varBits; ob++) {
	  const byteIdx = 49 + (ob >> 3), bitIdx = ob & 7;
	  const b_col = allB.map(bytes => (bytes[byteIdx] >> bitIdx) & 1);
	  const sol = solvGF2(A, b_col);
	  if (!sol) { inc++; masks.push(0n); }
	  else masks.push(maskBitsToNum(sol));
	}
	process.stderr.write(name + ': inconsistent bits = ' + inc + '\n');

	const pfxBuf = Buffer.from(pfx), sfxBuf = Buffer.from(sfx);
	let pass = 0, fail = 0;
	for (const [id, expected] of oracles) {
	  const bytes = Buffer.alloc(pfxBuf.length + (varBits / 8) + sfxBuf.length);
	  pfxBuf.copy(bytes);
	  const fb = buildFB(id, charCount);
	  for (let ob = 0; ob < varBits; ob++) {
		let p = 0;
		for (let b = 0; b < fb.length; b++) if (fb[b] && ((masks[ob] >> BigInt(b)) & 1n)) p ^= 1;
		if (p) bytes[49 + (ob >> 3)] |= (1 << (ob & 7));
	  }
	  sfxBuf.copy(bytes, 49 + (varBits / 8));
	  const got = bytes.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	  if (got === expected) pass++;
	  else { fail++; process.stderr.write('  FAIL ' + id + '\n'); }
	}
	process.stderr.write(name + ': ' + pass + '/' + oracles.length + ' pass, ' + fail + ' fail\n');
	return pass === oracles.length ? { masks, pfx, sfx } : null;
  }

  // Split into short (<=4 chars) and long (5 chars) groups
  const allOracles = [...tokens.entries()];
  const shortOracles = allOracles.filter(([id]) => id.length <= 4);
  const longOracles = allOracles.filter(([id]) => id.length === 5);
  process.stderr.write('Short IDs: ' + shortOracles.length + ', Long IDs: ' + longOracles.length + '\n');

  const shortResult = solveGroup('Short (4-char)', shortOracles, 4, 32);
  const longResult = solveGroup('Long (5-char)', longOracles, 5, 40);

  const out = {
	shortResult: shortResult ? {
	  prefix: shortResult.pfx,
	  suffix: shortResult.sfx,
	  masks: shortResult.masks.map(n => n.toString()),
	} : null,
	longResult: longResult ? {
	  prefix: longResult.pfx,
	  suffix: longResult.sfx,
	  masks: longResult.masks.map(n => n.toString()),
	} : null,
  };

  if (longResult) {
	process.stderr.write('\n=== Long masks (LiveVariableBitMasks, 40 elements) ===\n');
	for (let i = 0; i < 40; i += 8)
	  process.stderr.write('  ' + longResult.masks.slice(i, i + 8).map(n => n.toString()).join(', ') + ',\n');
	process.stderr.write('Long suffix: ' + JSON.stringify(longResult.sfx) + '\n');
  }
  if (shortResult) {
	process.stderr.write('\n=== Short masks (LiveVariableBitMasks4, 32 elements) ===\n');
	for (let i = 0; i < 32; i += 8)
	  process.stderr.write('  ' + shortResult.masks.slice(i, i + 8).map(n => n.toString()).join(', ') + ',\n');
	process.stderr.write('Short suffix: ' + JSON.stringify(shortResult.sfx) + '\n');
  }

  process.stdout.write(JSON.stringify(out) + '\n');

  if (!longResult || !shortResult) {
	process.exit(1);
  }
})();
