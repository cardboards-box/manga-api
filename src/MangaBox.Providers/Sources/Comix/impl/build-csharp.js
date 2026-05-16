'use strict';
// Build the complete lookup tables and output C# code for ComixToSigner
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('large-corpus.json', 'utf8'));
// Also merge fresh-corpus for completeness
const fresh = JSON.parse(fs.readFileSync('fresh-corpus.json', 'utf8'));
const allEntries = [...corpus];
const seen = new Set(corpus.map(c => c.id));
for (const e of fresh) if (!seen.has(e.id)) { allEntries.push(e); seen.add(e.id); }

// Add the 8w6dm capture
const target = require('./capture-8w6dm.json');
if (target.events && target.events.length > 0) {
  const ev = target.events[0];
  const tok = ev.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
  if (!seen.has('8w6dm')) allEntries.push({ id: '8w6dm', bytes: ev.bytes, token: tok });
  console.log('Added 8w6dm from capture');
}

const PREFIX_LEN = 49;
const long5 = allEntries.filter(d => d.id.length === 5);
const short4 = allEntries.filter(d => d.id.length <= 4);

console.log(`Long: ${long5.length}, Short: ${short4.length}`);

const CHARS = '0123456789abcdefghijklmnopqrstuvwxyz';
const CHAR_IDX = {};
CHARS.split('').forEach((c, i) => CHAR_IDX[c] = i);
const NC = 36;

function buildTable(items, varCount) {
  // tables[pos][charIdx] = byte value (or -1 if unknown)
  const tables = Array.from({ length: varCount }, () => new Array(NC).fill(-1));
  let conflicts = 0;
  for (const { id, bytes } of items) {
	for (let p = 0; p < varCount && p < id.length; p++) {
	  const ci = CHAR_IDX[id[p]];
	  if (ci === undefined) continue;
	  const v = bytes[PREFIX_LEN + p];
	  if (tables[p][ci] === -1) tables[p][ci] = v;
	  else if (tables[p][ci] !== v) { conflicts++; console.log(`Conflict: pos=${p} char=${id[p]} old=${tables[p][ci]} new=${v} id=${id}`); }
	}
  }
  console.log('Conflicts:', conflicts);
  return tables;
}

function countMissing(tables) {
  let m = 0;
  for (const tbl of tables) for (const v of tbl) if (v === -1) m++;
  return m;
}

const T5 = buildTable(long5, 5);
const T4 = buildTable(short4, 4);
console.log('Missing in T5:', countMissing(T5), '/ ', 5*NC);
console.log('Missing in T4:', countMissing(T4), '/ ', 4*NC);

// Verify on all samples
function verify(items, tables, varCount) {
  const PREFIX = items[0].bytes.slice(0, PREFIX_LEN);
  const SUFFIX = items[0].bytes.slice(PREFIX_LEN + varCount);
  let pass = 0, skip = 0, fail = 0;
  for (const { id, bytes, token } of items) {
	const varBytes = [];
	let ok = true;
	for (let p = 0; p < varCount; p++) {
	  const ci = CHAR_IDX[id[p]];
	  if (ci === undefined || tables[p][ci] === -1) { ok = false; break; }
	  varBytes.push(tables[p][ci]);
	}
	if (!ok) { skip++; continue; }
	const out = [...PREFIX, ...varBytes, ...SUFFIX];
	const got = Buffer.from(out).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	if (got === token) pass++;
	else { fail++; if (fail <= 3) console.log(`FAIL ${id}`); }
  }
  return { pass, skip, fail };
}

const r5 = verify(long5, T5, 5);
const r4 = verify(short4, T4, 4);
console.log(`T5 verify: ${r5.pass} pass, ${r5.skip} skip, ${r5.fail} fail`);
console.log(`T4 verify: ${r4.pass} pass, ${r4.skip} skip, ${r4.fail} fail`);

// Test 8w6dm
const PREFIX5 = long5[0].bytes.slice(0, PREFIX_LEN);
const SUFFIX5 = long5[0].bytes.slice(PREFIX_LEN + 5);
const testVarBytes = '8w6dm'.split('').map((c, p) => T5[p][CHAR_IDX[c]]);
const testOut = [...PREFIX5, ...testVarBytes, ...SUFFIX5];
const testTok = Buffer.from(testOut).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
console.log('\n8w6dm:', testTok);
console.log('Browser: YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
console.log('Match:', testTok === 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');

// Print C# code
console.log('\n// ======= C# ComixToSigner replacement =======');
console.log(`// PREFIX: ${PREFIX5.length} bytes`);
console.log(`private static readonly byte[] LiveTokenPrefix = new byte[] { ${PREFIX5.join(', ')} };`);
console.log(`private static readonly byte[] LiveTokenSuffix = new byte[] { ${SUFFIX5.join(', ')} };`);

const PREFIX4 = short4[0].bytes.slice(0, PREFIX_LEN);
const SUFFIX4 = short4[0].bytes.slice(PREFIX_LEN + 4);
console.log(`private static readonly byte[] LiveTokenSuffix4 = new byte[] { ${SUFFIX4.join(', ')} };`);

// T5 as flat array: table[pos * 36 + charIdx] 
// We'll use 0 for unknown chars (will fail gracefully)
const t5flat = [];
for (let p = 0; p < 5; p++) for (let c = 0; c < NC; c++) t5flat.push(T5[p][c] === -1 ? 0 : T5[p][c]);
console.log(`\n// Long (5-char) positional table [5 positions x 36 chars]`);
console.log(`private static readonly byte[] LiveTable5 = new byte[]\n{`);
for (let p = 0; p < 5; p++) {
  const row = Array.from({ length: NC }, (_, c) => T5[p][c] === -1 ? 0 : T5[p][c]);
  console.log(`\t// pos ${p}: chars "${CHARS}"`);
  console.log(`\t${row.join(', ')},`);
}
console.log(`};`);

const t4flat = [];
for (let p = 0; p < 4; p++) for (let c = 0; c < NC; c++) t4flat.push(T4[p][c] === -1 ? 0 : T4[p][c]);
console.log(`\n// Short (4-char) positional table [4 positions x 36 chars]`);
console.log(`private static readonly byte[] LiveTable4 = new byte[]\n{`);
for (let p = 0; p < 4; p++) {
  const row = Array.from({ length: NC }, (_, c) => T4[p][c] === -1 ? 0 : T4[p][c]);
  console.log(`\t// pos ${p}: chars "${CHARS}"`);
  console.log(`\t${row.join(', ')},`);
}
console.log(`};`);

console.log(`\n// Char to index: '0'->0 .. '9'->9, 'a'->10 .. 'z'->35`);
console.log(`// Usage: byte b = LiveTable5[pos * 36 + CharToIdx(id[pos])];`);
