'use strict';
const fs = require('fs');
const text = fs.readFileSync('ts_build_35595e3de3c99889c1aa70_dist_main_teyj2t_Ba7VRWLS_js.js', 'utf8');

// Find all occurrences of _= or {_: or query param generation
const searches = ['secureGet', 'secure', 'signRequest', 'param', 'query'];
for (const s of searches) {
  const idx = text.indexOf(s);
  if (idx >= 0) {
	console.log('FOUND', s, 'at', idx);
	console.log(text.slice(Math.max(0, idx-50), idx+200));
	console.log('---');
  }
}

// Look for calls to chapters with extra params
const re = /chapters[^}]{0,300}page|page[^}]{0,200}chapters/g;
let m;
while ((m = re.exec(text)) !== null) {
  console.log('CHAPTERS+PAGE at', m.index, ':', m[0].slice(0, 200));
}

// Look for anything base64-like being assigned to a request param
const re2 = /\bparams\b[^;]{0,500}/g;
while ((m = re2.exec(text)) !== null) {
  if (/_|sign|token/i.test(m[0])) {
	console.log('PARAMS match at', m.index, ':', m[0].slice(0, 200));
  }
}
