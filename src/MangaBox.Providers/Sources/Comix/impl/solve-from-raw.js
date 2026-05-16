'use strict';
// Solve the signer from raw byte arrays captured directly from browser
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('fresh-corpus.json', 'utf8'));

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
  console.log('Prefix bytes:', JSON.stringify(prefix));
  console.log('Suffix bytes:', JSON.stringify(suffix));

  // Verify all items have consistent prefix/suffix
  let pfxOk = true, sfxOk = true;
  for (const b of allB) {
	for (let i = 0; i < prefix.length; i++) if (b[i] !== prefix[i]) { pfxOk = false; }
	for (let i = 0; i < suffix.length; i++) if (b[varEnd+i] !== suffix[i]) { sfxOk = false; }
  }
  console.log('Prefix consistent:', pfxOk, 'Suffix consistent:', sfxOk);

  const A = items.map(({ id }) => buildFB(id, charCount));
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

  // Verify reconstruction
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
	else { fail++; if (fail <= 3) console.log('FAIL', id, '\n  exp:', token.slice(60), '\n  got:', gotB64.slice(60)); }
  }
  console.log('Pass:', pass + '/' + items.length);

  if (pass === items.length) {
	console.log('\n=== C# CONSTANTS FOR', name, '===');
	console.log('// Prefix (' + prefix.length + ' bytes)');
	console.log('new byte[] { ' + prefix.join(', ') + ' }');
	console.log('// Suffix (' + suffix.length + ' bytes)');
	console.log('new byte[] { ' + suffix.join(', ') + ' }');
	console.log('// Masks (' + varBits + ' elements)');
	for (let i = 0; i < varBits; i++) {
	  process.stdout.write('\t\t\t\t' + masks[i].toString() + (i < varBits-1 ? ',' : '') + '\n');
	}
  }
  return { masks, prefix, suffix, pass, total: items.length };
}

// Short (4-char): 32 var bits = 4 bytes, bytes 49..52, suffix 53..63
const shortRes = solveGroup('Short (4-char)', shortCorpus, 4, 49, 53);
// Long (5-char): 40 var bits = 5 bytes, bytes 49..53, suffix 54..64
const longRes = solveGroup('Long (5-char)', longCorpus, 5, 49, 54);
