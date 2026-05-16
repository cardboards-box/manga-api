'use strict';
// Test hypothesis: var_byte[p] = f_p(id[p]) — each variable byte depends ONLY on id[p]
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('large-corpus.json', 'utf8'));

const PREFIX_LEN = 49;
const long5 = corpus.filter(d => d.id.length === 5);
const short4 = corpus.filter(d => d.id.length <= 4);

function testPositionalModel(name, items, varCount) {
  console.log(`\n=== ${name} (${items.length} samples) ===`);

  // Group by character at each position
  const tables = []; // tables[pos][char] = Set of observed byte values
  for (let p = 0; p < varCount; p++) {
	const tbl = {};
	for (const { id, bytes } of items) {
	  const c = id[p] || '\0';
	  const v = bytes[PREFIX_LEN + p];
	  if (!tbl[c]) tbl[c] = new Set();
	  tbl[c].add(v);
	}
	tables.push(tbl);
  }

  // Check if each position's table is deterministic
  let consistent = true;
  for (let p = 0; p < varCount; p++) {
	for (const [c, vals] of Object.entries(tables[p])) {
	  if (vals.size > 1) {
		consistent = false;
		console.log(`Position ${p}, char '${c}': multiple values ${[...vals]}`);
	  }
	}
  }
  console.log('Positional model consistent:', consistent);

  if (consistent) {
	// Build lookup tables
	const lookupTables = tables.map(tbl => {
	  const map = {};
	  for (const [c, vals] of Object.entries(tbl)) map[c] = [...vals][0];
	  return map;
	});

	// Count chars observed at each position
	for (let p = 0; p < varCount; p++) {
	  const chars = Object.keys(tables[p]).sort().join('');
	  console.log(`Position ${p}: ${Object.keys(tables[p]).length} chars: [${chars}]`);
	}

	// Verify reconstruction on all samples
	let pass = 0, fail = 0;
	for (const { id, bytes, token } of items) {
	  const PREFIX = bytes.slice(0, PREFIX_LEN);
	  const SUFFIX = bytes.slice(PREFIX_LEN + varCount);
	  const varBytes = [];
	  let ok = true;
	  for (let p = 0; p < varCount; p++) {
		const c = id[p] || '\0';
		if (lookupTables[p][c] === undefined) { ok = false; break; }
		varBytes.push(lookupTables[p][c]);
	  }
	  if (!ok) { fail++; continue; }
	  const out = [...PREFIX, ...varBytes, ...SUFFIX];
	  const got = Buffer.from(out).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	  if (got === token) pass++;
	  else { fail++; console.log(`FAIL ${id}: ${token.slice(56)} vs ${got.slice(56)}`); }
	}
	console.log(`Reconstruction: ${pass}/${items.length}`);

	// Try 8w6dm
	const testId = '8w6dm';
	const hasAll = testId.split('').every((c, p) => lookupTables[p][c] !== undefined);
	if (hasAll && varCount === 5) {
	  const PREFIX = items[0].bytes.slice(0, PREFIX_LEN);
	  const SUFFIX = items[0].bytes.slice(PREFIX_LEN + varCount);
	  const varBytes = testId.split('').map((c, p) => lookupTables[p][c]);
	  const out = [...PREFIX, ...varBytes, ...SUFFIX];
	  const tok = Buffer.from(out).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	  console.log(`8w6dm token: ${tok}`);
	  console.log(`URL token:   YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y`);
	} else if (varCount === 5) {
	  console.log('8w6dm: missing chars at positions', testId.split('').map((c,p) => lookupTables[p][c] === undefined ? p : null).filter(x=>x!==null));
	}

	return lookupTables;
  }
  return null;
}

const T5 = testPositionalModel('Long 5-char', long5, 5);
const T4 = testPositionalModel('Short 4-char', short4, 4);

// Print the lookup tables for C# if solved
if (T5) {
  console.log('\n=== C# LONG TABLE ===');
  const chars = '0123456789abcdefghijklmnopqrstuvwxyz';
  for (let p = 0; p < 5; p++) {
	const vals = chars.split('').map(c => T5[p][c] !== undefined ? T5[p][c] : '?');
	console.log(`// Position ${p}: { ${vals.join(', ')} }`);
  }
}
if (T4) {
  console.log('\n=== C# SHORT TABLE ===');
  const chars = '0123456789abcdefghijklmnopqrstuvwxyz';
  for (let p = 0; p < 4; p++) {
	const vals = chars.split('').map(c => T4[p][c] !== undefined ? T4[p][c] : '?');
	console.log(`// Position ${p}: { ${vals.join(', ')} }`);
  }
}
