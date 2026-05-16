'use strict';
// Analyze variable bytes to find the true signer structure
// Hypothesis: variable_bytes = T[id[0]] XOR T[id[1]] XOR T[id[2]] XOR T[id[3]] XOR T[id[4]]
// where T is a per-character lookup table (one 5-byte entry per character in [0-9a-z])
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('large-corpus.json', 'utf8'));

const PREFIX_LEN = 49;
const VAR5 = 5, VAR4 = 4;

// Extract variable bytes for each entry
const entries = corpus.map(({ id, bytes, token }) => {
  const varLen = id.length <= 4 ? VAR4 : VAR5;
  const varBytes = bytes.slice(PREFIX_LEN, PREFIX_LEN + varLen);
  return { id, varBytes, varLen };
});

console.log('Sample var bytes:');
entries.slice(0, 10).forEach(({ id, varBytes }) =>
  console.log(`  ${id}: [${varBytes.join(',')}]`)
);

// XOR two variable byte arrays
function xorBytes(a, b) {
  return a.map((v, i) => v ^ b[i]);
}

// Test hypothesis: T[id[j]] = XOR of contributions from char j
// First, find ID pairs that share 4/5 characters to isolate single-char differences
const long5 = entries.filter(e => e.varLen === 5);
console.log(`\nLooking for pairs sharing 4/5 chars among ${long5.length} 5-char entries...`);

const pairs4common = [];
for (let i = 0; i < long5.length; i++) {
  for (let j = i+1; j < long5.length; j++) {
	const a = long5[i].id, b = long5[j].id;
	let diffCount = 0, diffPos = -1;
	for (let k = 0; k < 5; k++) {
	  if (a[k] !== b[k]) { diffCount++; diffPos = k; }
	}
	if (diffCount === 1) {
	  pairs4common.push({ a: long5[i], b: long5[j], diffPos, charA: a[diffPos], charB: b[diffPos] });
	}
  }
}
console.log(`Found ${pairs4common.length} pairs with exactly 1 differing char`);
pairs4common.slice(0, 5).forEach(({ a, b, diffPos, charA, charB }) => {
  const diff = xorBytes(a.varBytes, b.varBytes);
  console.log(`  ${a.id} vs ${b.id} (pos ${diffPos}: '${charA}' vs '${charB}'): XOR=[${diff.join(',')}]`);
});

// Build a table using XOR chains
// Start with one reference ID and XOR to find relative table values
// T[c] relative to T[id0[pos]] for each position

// Better approach: for each position p, collect all IDs
// Group by all chars EXCEPT position p, then XOR pairs to get T[c] XOR T[c'] for that position

// Try to reconstruct table using a spanning tree approach
// Char mapping: 0-9 → 0-9, a-z → 10-35
function charToIdx(c) {
  if (c >= '0' && c <= '9') return c.charCodeAt(0) - 48;
  return c.charCodeAt(0) - 87; // a=10, b=11, ...z=35
}

const CHAR_SET = '0123456789abcdefghijklmnopqrstuvwxyz';
const TABLE = Array.from({ length: 36 }, () => null); // 5 bytes each, but only what we can determine

// For each pair with 1 char difference, we get: TABLE[charA][*] XOR TABLE[charB][*] = XOR of variable bytes
// This gives us relative table entries

const relConstraints = new Map(); // key: "idxA,idxB" → XOR value
for (const { charA, charB, a, b, diffPos } of pairs4common) {
  const idxA = charToIdx(charA), idxB = charToIdx(charB);
  const key = `${Math.min(idxA,idxB)},${Math.max(idxA,idxB)}`;
  const diff = xorBytes(a.varBytes, b.varBytes);
  if (!relConstraints.has(key)) relConstraints.set(key, diff);
}
console.log(`\nUnique relative constraints: ${relConstraints.size}`);

// Check if the XOR structure is consistent (same pair always gives same XOR regardless of context)
// Group by character pairs: for same (charA, charB), is the XOR always the same?
const pairXors = new Map();
let consistent = true;
for (const { charA, charB, a, b, diffPos } of pairs4common) {
  const idxA = charToIdx(charA), idxB = charToIdx(charB);
  const key = `${Math.min(idxA,idxB)},${Math.max(idxA,idxB)}`;
  const diff = xorBytes(a.varBytes, b.varBytes);
  const diffStr = diff.join(',');
  if (!pairXors.has(key)) {
	pairXors.set(key, { diff, diffStr, examples: [] });
  } else {
	const existing = pairXors.get(key);
	if (existing.diffStr !== diffStr) {
	  consistent = false;
	  console.log(`INCONSISTENT: ${charA} vs ${charB} at different positions:`);
	  console.log(`  Example 1: ${existing.examples[0].a.id} vs ${existing.examples[0].b.id} → [${existing.diffStr}]`);
	  console.log(`  Example 2: ${a.id} vs ${b.id} → [${diffStr}]`);
	}
  }
  pairXors.get(key).examples.push({ a, b, diffPos });
}
console.log(`XOR structure consistent across all same-char pairs: ${consistent}`);

// Now check if the structure is position-independent
// i.e., does T[c] XOR T[c'] give the same value regardless of which position the chars are in?
const posXors = new Map(); // "charA,charB" → Map<pos → xorStr>
for (const { charA, charB, a, b, diffPos } of pairs4common) {
  const key = `${charA},${charB}`;
  if (!posXors.has(key)) posXors.set(key, new Map());
  const diff = xorBytes(a.varBytes, b.varBytes);
  posXors.get(key).set(diffPos, diff.join(','));
}
let posConsistent = true;
for (const [chars, posMap] of posXors) {
  if (posMap.size > 1) {
	const vals = [...posMap.values()];
	if (new Set(vals).size > 1) {
	  posConsistent = false;
	  console.log(`Position-dependent: ${chars} at positions ${[...posMap.keys()].join(',')} gives different XORs: ${vals.join(' | ')}`);
	}
  }
}
console.log(`XOR is position-independent: ${posConsistent}`);

// Summary: does the T[c] XOR model hold?
console.log('\n=== SUMMARY ===');
console.log('If "consistent AND position-independent": simple XOR table applies');
console.log('If "consistent BUT position-dependent": table has per-position variants T_pos[c]');
console.log('If "inconsistent": function is not XOR-decomposable');
