const fs = require('fs');
const b = fs.readFileSync('current-secure-bundle.js', 'utf8');

// The token variable region is 5 bytes computed from ID.
// The prefix is 49 FIXED bytes. Let's check if the prefix bytes
// appear as integer arrays.
// First byte of prefix is 97 (char 'a')
// Look for patterns like [97, or ,97, with neighbors 200 or 64

// Search for 97 near 200
let found = 0;
for (let i = 0; i < b.length - 20; i++) {
  if (b[i] === '9' && b[i+1] === '7') {
    const chunk = b.substring(i-1, i+15);
    if (/[,\[]97,200/.test(chunk)) {
      console.log('Found at', i, ':', b.substring(i-20, i+100));
      found++;
      if (found >= 5) break;
    }
  }
}
if (!found) console.log('Not found as integer array');

// What about split across lines / spaces?
const idx = b.search(/97\s*,\s*200/);
console.log('97,200 with spaces at:', idx);
if (idx >= 0) console.log(b.substring(idx-30, idx+100));
