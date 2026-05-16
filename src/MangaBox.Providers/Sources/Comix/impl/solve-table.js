'use strict';
// Solve using the correct XOR-table model:
// var_bytes[id] = T[id[0]] XOR T[id[1]] XOR ... XOR T[id[n-1]]
// T[c] is a per-character lookup table entry (4 or 5 bytes depending on ID length)
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('large-corpus.json', 'utf8'));

const PREFIX_LEN = 49;
const CHAR_SET = '0123456789abcdefghijklmnopqrstuvwxyz';
const CHAR_IDX = {};
CHAR_SET.split('').forEach((c, i) => CHAR_IDX[c] = i);
const NC = CHAR_SET.length; // 36

function charFreqRow(id) {
  // A[c] = (count of c in id) mod 2
  const row = new Array(NC).fill(0);
  for (const c of id) row[CHAR_IDX[c]] ^= 1;
  return row;
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

function solveGroup(name, items, varCount) {
  console.log(`\n--- ${name} (${items.length} items) ---`);

  const PREFIX = items[0].bytes.slice(0, PREFIX_LEN);
  const SUFFIX = items[0].bytes.slice(PREFIX_LEN + varCount);

  // Verify prefix/suffix consistency
  let ok = true;
  for (const { id, bytes } of items) {
	for (let i = 0; i < PREFIX_LEN; i++) if (bytes[i] !== PREFIX[i]) { ok = false; console.log(`Prefix mismatch: ${id}[${i}] = ${bytes[i]}`); }
	for (let i = 0; i < SUFFIX.length; i++) if (bytes[PREFIX_LEN + varCount + i] !== SUFFIX[i]) { ok = false; console.log(`Suffix mismatch: ${id}[${i}] = ${bytes[PREFIX_LEN + varCount + i]}`); }
  }
  console.log('Prefix/suffix consistent:', ok);

  const A = items.map(({ id }) => charFreqRow(id));

  // Check rank
  const rankMat = A.map(r => [...r]);
  let rank = 0;
  for (let col = 0; col < NC && rank < rankMat.length; col++) {
	let pr = -1;
	for (let r = rank; r < rankMat.length; r++) if (rankMat[r][col] === 1) { pr = r; break; }
	if (pr < 0) continue;
	[rankMat[rank], rankMat[pr]] = [rankMat[pr], rankMat[rank]];
	for (let r = 0; r < rankMat.length; r++) {
	  if (r !== rank && rankMat[r][col] === 1)
		for (let c = col; c < NC; c++) rankMat[r][c] ^= rankMat[rank][c];
	}
	rank++;
  }
  console.log(`Matrix rank: ${rank} / ${NC} (need ${NC} for unique table; have ${items.length} equations)`);

  // Solve for each bit of each variable byte
  const varBits = varCount * 8;
  let inc = 0;
  const tableFlat = []; // tableFlat[charIdx * varBits + bitIdx] = table bit

  // For each bit, solve the system
  const bitSols = [];
  for (let b = 0; b < varBits; b++) {
	const bytePos = b >> 3, bitPos = b & 7;
	const rhs = items.map(({ bytes }) => (bytes[PREFIX_LEN + bytePos] >> bitPos) & 1);
	const sol = solvGF2(A, rhs);
	if (!sol) { inc++; bitSols.push(new Array(NC).fill(0)); }
	else bitSols.push(sol);
  }
  console.log(`Inconsistent bits: ${inc} / ${varBits}`);

  // Reconstruct table: T[c] = varCount bytes, bit b = bitSols[b][c]
  const T = [];
  for (let c = 0; c < NC; c++) {
	const entry = new Array(varCount).fill(0);
	for (let b = 0; b < varBits; b++) {
	  if (bitSols[b][c]) entry[b >> 3] |= (1 << (b & 7));
	}
	T.push(entry);
  }

  // Verify
  let pass = 0, fail = 0;
  for (const { id, bytes, token } of items) {
	const varBytes = new Array(varCount).fill(0);
	for (const c of id) {
	  const tEntry = T[CHAR_IDX[c]];
	  for (let i = 0; i < varCount; i++) varBytes[i] ^= tEntry[i];
	}
	const out = [...PREFIX, ...varBytes, ...SUFFIX];
	const got = Buffer.from(out).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	if (got === token) pass++;
	else { fail++; if (fail <= 3) console.log(`FAIL ${id}: exp[60+]=${token.slice(60)} got[60+]=${got.slice(60)}`); }
  }
  console.log(`Pass: ${pass}/${items.length}`);

  // Test unknown IDs
  const testIds = ['8w6dm', '5r7m', '936j'];
  console.log('\nPredictions for unknown IDs:');
  for (const id of testIds) {
	if (!id.split('').every(c => CHAR_IDX[c] !== undefined)) { console.log(`  ${id}: contains unknown char`); continue; }
	if ((id.length <= 4) !== (varCount === VAR_SHORT)) continue;
	const varBytes = new Array(varCount).fill(0);
	for (const c of id) {
	  const tEntry = T[CHAR_IDX[c]];
	  for (let i = 0; i < varCount; i++) varBytes[i] ^= tEntry[i];
	}
	const out = [...PREFIX, ...varBytes, ...SUFFIX];
	const tok = Buffer.from(out).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	console.log(`  ${id}: ${tok}`);
  }

  // Output C# table
  if (pass === items.length || inc === 0) {
	console.log('\n=== C# TABLE ===');
	console.log(`// ${name} table: T[charIdx] for charIdx = index in "${CHAR_SET}"`);
	console.log(`// ${varCount}-byte entries, 36 chars`);
	const rows = T.map((entry, i) => `\t\t\t/* '${CHAR_SET[i]}' */ new byte[] { ${entry.join(', ')} },`);
	rows.forEach(r => console.log(r));
  }
  return T;
}

const VAR_SHORT = 4;
const VAR_LONG = 5;
const long5 = corpus.filter(d => d.id.length === 5);
const short4 = corpus.filter(d => d.id.length <= 4);

const T5 = solveGroup('Long (5-char)', long5, VAR_LONG);
const T4 = solveGroup('Short (4-char)', short4, VAR_SHORT);

// Compute token for 8w6dm using T5
const PREFIX = long5[0].bytes.slice(0, PREFIX_LEN);
const SUFFIX5 = long5[0].bytes.slice(PREFIX_LEN + VAR_LONG);
const id = '8w6dm';
const varBytes = new Array(VAR_LONG).fill(0);
for (const c of id) {
  const tEntry = T5[CHAR_IDX[c]];
  for (let i = 0; i < VAR_LONG; i++) varBytes[i] ^= tEntry[i];
}
const out = [...PREFIX, ...varBytes, ...SUFFIX5];
const tok = Buffer.from(out).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
console.log('\n8w6dm token:', tok);
console.log('User URL tok: YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y');
