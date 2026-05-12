const fs = require('fs');

const d = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-d-oracle.json', 'utf8'));
const r = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));

const d5 = d.calls[5];
const r0 = r.calls[0];

console.log('d5 len', d5.length, 'r0 len', r0.length);

let bestShift = 0;
let bestMatches = -1;

for (let shift = 0; shift <= d5.length - r0.length; shift++) {
  let matches = 0;
  for (let i = 0; i < r0.length; i++) {
	if (d5[shift + i] === r0[i]) matches++;
  }
  if (matches > bestMatches) {
	bestMatches = matches;
	bestShift = shift;
  }
}

console.log('bestShift', bestShift, 'bestMatches', bestMatches, 'ratio', (bestMatches / r0.length).toFixed(4));
console.log('d5 head@shift', d5.slice(bestShift, bestShift + 32).join(','));
console.log('r0 head', r0.slice(0, 32).join(','));
