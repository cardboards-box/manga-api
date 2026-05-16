'use strict';
// Find which characters are missing from each position's table,
// then propose IDs to visit to fill them in
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('large-corpus.json', 'utf8'));

const PREFIX_LEN = 49;
const CHARS = '0123456789abcdefghijklmnopqrstuvwxyz';
const long5 = corpus.filter(d => d.id.length === 5);
const short4 = corpus.filter(d => d.id.length <= 4);

function getMissingChars(items, varCount) {
  const seen = Array.from({ length: varCount }, () => new Set());
  for (const { id } of items) {
	for (let p = 0; p < varCount && p < id.length; p++) seen[p].add(id[p]);
  }
  const missing = [];
  for (let p = 0; p < varCount; p++) {
	missing.push(CHARS.split('').filter(c => !seen[p].has(c)));
  }
  return missing;
}

const miss5 = getMissingChars(long5, 5);
const miss4 = getMissingChars(short4, 4);

console.log('Missing chars in 5-char table:');
miss5.forEach((m, p) => console.log(`  pos ${p}: [${m.join('')}] (${m.length} missing)`));
console.log('\nMissing chars in 4-char table:');
miss4.forEach((m, p) => console.log(`  pos ${p}: [${m.join('')}] (${m.length} missing)`));

// Check specifically what 8w6dm needs
const testId = '8w6dm';
console.log('\n8w6dm needs:');
for (let p = 0; p < testId.length; p++) {
  const c = testId[p];
  if (miss5[p].includes(c)) console.log(`  pos ${p}: '${c}' MISSING`);
  else console.log(`  pos ${p}: '${c}' ok`);
}

// Suggest IDs from the known ID list on comix that would fill gaps
// We need 5-char IDs containing specific chars at specific positions
// Load the full ID list if we have it
