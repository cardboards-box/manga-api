'use strict';
const fs = require('fs');
const text = fs.readFileSync('_build_35595e3de3c99889c1aa70_dist_secure_teyj2t_DpfA5hYx_js.js', 'utf8');

// JQ is at 197576, J8 is at 216678
// Extract surrounding function context (several KB around each)
const jqCtx = text.slice(197400, 198200);
const j8Ctx = text.slice(216400, 217000);

// Also find the function boundaries
function findFnBefore(text, pos) {
  // Walk back to find 'function JQ' or 'JQ='
  for (let i = pos; i > pos - 5000 && i > 0; i--) {
	if (text.slice(i, i+12) === 'function JQ(' || text.slice(i, i+4) === 'JQ=') return i;
  }
  return pos - 200;
}

console.log('=== JQ area (btoa caller) at 197576 ===');
console.log(jqCtx);
console.log('\n=== J8 area (JQ caller) at 216678 ===');
console.log(j8Ctx);

// Find all JQ references
let idx = 0;
while (true) {
  const i = text.indexOf('function JQ', idx);
  if (i < 0) break;
  console.log('\nfunction JQ at', i);
  console.log(text.slice(i, i+500));
  idx = i + 10;
}
