const fs = require('fs');

const d = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-d-oracle.json', 'utf8'));
const r = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));

console.log('D lengths', d.callLengths);
console.log('R lengths', r.callLengths);

for (let i = 0; i < d.calls.length; i++) {
  const arr = d.calls[i];
  const head = arr.slice(0, 32);
  console.log('D', i, 'len', arr.length, 'head', head.join(','));
}

for (let i = 0; i < r.calls.length; i++) {
  const arr = r.calls[i];
  const head = arr.slice(0, 32);
  console.log('R', i, 'len', arr.length, 'head', head.join(','));
}

console.log('R out head', r.outputHead);
console.log('D out head', d.outputHead);
