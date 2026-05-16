'use strict';
const fs = require('fs');
const text = fs.readFileSync('_build_35595e3de3c99889c1aa70_dist_secure_teyj2t_DpfA5hYx_js.js', 'utf8');

// Find ALL occurrences of 'params' near underscore '_'
const re = /params[^;}{]{0,200}/g;
let m;
let count = 0;
while ((m = re.exec(text)) !== null && count < 30) {
  if (m[0].includes('_') || m[0].includes('sign') || m[0].includes('token')) {
	console.log('PARAMS at', m.index, ':', m[0].slice(0, 200));
	count++;
  }
}

// Find the request.params assignment / axios interceptor
const reqIdx = text.indexOf('request.params');
if (reqIdx >= 0) {
  console.log('\n=== request.params at', reqIdx, '===');
  console.log(text.slice(Math.max(0, reqIdx - 200), reqIdx + 500));
}

// find getSign or similar
const patterns = ['getSign', 'sign(', 'genToken', 'buildToken', 'makeToken', 'hashId', 'signId'];
for (const p of patterns) {
  const i = text.indexOf(p);
  if (i >= 0) {
	console.log('\n=== ' + p + ' at', i, '===');
	console.log(text.slice(Math.max(0, i-50), i+300));
  }
}

// Look for the specific bytes in the prefix we know: 97,200,64,...
// The prefix as base64 would start with "YchAkKI..."
const pfxB64 = 'YchAkKI';
const pfxIdx = text.indexOf(pfxB64);
if (pfxIdx >= 0) {
  console.log('\n=== PREFIX BASE64 found at', pfxIdx, '===');
  console.log(text.slice(Math.max(0, pfxIdx - 100), pfxIdx + 300));
} else {
  console.log('\nPrefix base64 NOT found directly');
  // Try to find it as raw bytes
  const pfxHex = [97,200,64,144,162,7].map(b => b.toString(16).padStart(2,'0')).join('');
  console.log('Looking for hex prefix:', pfxHex);
}

// Find axios interceptors
const axIdx = text.indexOf('axios');
if (axIdx >= 0) {
  console.log('\n=== axios at', axIdx, '===');
  console.log(text.slice(axIdx, axIdx + 1000));
}
