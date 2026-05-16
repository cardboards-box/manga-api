'use strict';
const fs = require('fs');
const text = fs.readFileSync('_build_35595e3de3c99889c1aa70_dist_secure_teyj2t_DpfA5hYx_js.js', 'utf8');
console.log('Size:', text.length);

// Find request interceptors
const interceptorIdx = text.indexOf('interceptors');
if (interceptorIdx >= 0) {
  console.log('\n=== interceptors ===');
  console.log(text.slice(Math.max(0, interceptorIdx - 50), interceptorIdx + 500));
}

// Find all function sections that mention _ param
let idx = 0;
let count = 0;
while (count < 20) {
  const i = text.indexOf('"_"', idx);
  if (i < 0) break;
  console.log('\n=== "_" at', i, '===');
  console.log(text.slice(Math.max(0, i - 100), i + 300));
  idx = i + 1;
  count++;
}

// Look for the token generator
const keywords = ['P0(', 'base64', 'Uint8Array', 'manga', 'request.params'];
for (const kw of keywords) {
  const ki = text.indexOf(kw);
  if (ki >= 0) {
	console.log('\n=== ' + kw + ' at ' + ki + ' ===');
	console.log(text.slice(Math.max(0, ki - 50), ki + 300));
  }
}
