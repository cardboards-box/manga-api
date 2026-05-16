const fs = require('fs');
const b = fs.readFileSync('current-secure-bundle.js', 'utf8');
// Show more context around _=
const idx = b.indexOf('_=');
console.log(b.substring(idx-200, idx+300));
