'use strict';
// Solve from large-corpus.json (80 five-char, 20 four-char)
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('large-corpus.json', 'utf8'));

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

const shortCorpus = corpus.filter(d => d.id.length <= 4);
const longCorpus = corpus.filter(d => d.id.length === 5);
console.log('Short:', shortCorpus.length, 'Long:', longCorpus.length);

function solveGroup(name, items, charCount, varStart, varEnd) {
  const varBits = (varEnd - varStart) * 8;
  const allB = items.map(d => d.bytes);
  const prefix = allB[0].slice(0, varStart);
  const suffix = allB[0].slice(varEnd);

  console.log('\n--- ' + name + ' ---');

  // Verify consistency
  let pfxOk = true, sfxOk = true;
  for (const b of allB) {
	for (let i = 0; i < prefix.length; i++) if (b[i] !== prefix[i]) pfxOk = false;
	for (let i = 0; i < suffix.length; i++) if (b[varEnd+i] !== suffix[i]) sfxOk = false;
  }
  console.log('Prefix consistent:', pfxOk, 'Suffix consistent:', sfxOk);
  if (!pfxOk || !sfxOk) {
	// Find and log mismatches
	for (const { id, bytes: b } of items) {
	  for (let i = 0; i < prefix.length; i++) {
		if (b[i] !== prefix[i]) console.log(`  ${id} prefix[${i}]: expected ${prefix[i]} got ${b[i]}`);
	  }
	}
  }

  const A = items.map(({ id }) => buildFB(id, charCount));

  // Check rank
  const rankMat = A.map(row => [...row]);
  let rank = 0;
  for (let col = 0; col < rankMat[0].length && rank < rankMat.length; col++) {
	let pr = -1;
	for (let r = rank; r < rankMat.length; r++) if (rankMat[r][col] === 1) { pr = r; break; }
	if (pr < 0) continue;
	[rankMat[rank], rankMat[pr]] = [rankMat[pr], rankMat[rank]];
	for (let r = 0; r < rankMat.length; r++) {
	  if (r !== rank && rankMat[r][col] === 1)
		for (let c = col; c < rankMat[0].length; c++) rankMat[r][c] ^= rankMat[rank][c];
	}
	rank++;
  }
  console.log('Matrix rank:', rank, '/', charCount * 8, '(need', charCount * 8, 'for unique solution)');

  let inc = 0;
  const masks = [];
  for (let ob = 0; ob < varBits; ob++) {
	const byteIdx = varStart + (ob >> 3), bitIdx = ob & 7;
	const b_col = allB.map(b => (b[byteIdx] >> bitIdx) & 1);
	const sol = solvGF2(A, b_col);
	if (!sol) { inc++; masks.push(0n); }
	else masks.push(maskBitsToNum(sol));
  }
  console.log('Inconsistent bits:', inc, '/', varBits);

  // Verify reconstruction on corpus
  let pass = 0, fail = 0;
  for (const { id, bytes, token } of items) {
	const fb = buildFB(id, charCount);
	const out = [...prefix];
	for (let ob = 0; ob < varBits; ob++) {
	  if (ob % 8 === 0) out.push(0);
	  let p = 0;
	  for (let b = 0; b < fb.length; b++) if (fb[b] && ((masks[ob] >> BigInt(b)) & 1n)) p ^= 1;
	  if (p) out[out.length-1] |= (1 << (ob & 7));
	}
	out.push(...suffix);
	const gotB64 = Buffer.from(out).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	if (gotB64 === token) pass++;
	else { fail++; if (fail <= 3) console.log('FAIL', id, '\n  exp:', token, '\n  got:', gotB64); }
  }
  console.log('Pass:', pass + '/' + items.length);

  if (pass === items.length || inc === 0) {
	console.log('\n=== C# MASKS FOR', name, '===');
	for (let i = 0; i < varBits; i++) {
	  console.log('\t\t\t\t' + masks[i].toString() + (i < varBits-1 ? ',' : ''));
	}
  }
  return { masks, prefix, suffix, pass, total: items.length };
}

// Test against the known failing ID using current masks
const MASKS5_CURRENT = [1n,35n,17973325n,1332340n,20976474n,17189124n,4610622n,32n,256n,544n,1536n,17515604n,6245121n,6249217n,460377n,17826603n,197748n,16976482n,17957399n,21170754n,17192752n,65568n,196608n,21059884n,18092892n,22942580n,0n,18357311n,1595162n,5381143n,6101306n,1660195n,6248992n,18355534n,16977780n,1461359n,6243885n,6119274n,17700693n,18502400n];
const PREFIX = [97,200,64,144,162,7,176,70,112,166,46,172,221,0,253,31,196,10,25,32,99,136,29,229,210,13,150,51,132,252,213,72,16,222,85,16,49,197,175,230,100,6,120,233,32,249,167,132,106];
const SUFFIX5 = [127,241,120,206,4,167,103,234,234,27,134];

function signWithMasks(id, masks) {
  const varStart = 49, varCount = 5;
  const bytes = [...PREFIX, ...new Array(varCount).fill(0), ...SUFFIX5];
  let featureBits = 0n, bitIdx = 0;
  for (let i = 0; i < 5; i++) {
	const ch = BigInt(i < id.length ? id.charCodeAt(i) : 0);
	for (let b = 0; b < 8; b++) {
	  if ((ch >> BigInt(b)) & 1n) featureBits |= 1n << BigInt(bitIdx);
	  bitIdx++;
	}
  }
  for (let ob = 0; ob < masks.length; ob++) {
	let v = featureBits & masks[ob], p = 0n;
	while (v) { p ^= v & 1n; v >>= 1n; }
	if (p) bytes[varStart + (ob >> 3)] |= (1 << (ob & 7));
  }
  return Buffer.from(bytes).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
}

console.log('\nCurrent signer output for 8w6dm:', signWithMasks('8w6dm', MASKS5_CURRENT));
console.log('URL token from user:             YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y');

const longRes = solveGroup('Long (5-char)', longCorpus, 5, 49, 54);
const shortRes = solveGroup('Short (4-char)', shortCorpus, 4, 49, 53);

// Test the new masks against 8w6dm if we have them
if (longRes.masks.length === 40) {
  console.log('\nNew signer output for 8w6dm:', signWithMasks('8w6dm', longRes.masks));
}
