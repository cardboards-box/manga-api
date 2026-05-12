const fs = require('fs');

const file = 'MangaBox.Providers/Sources/Comix/impl/secure-teup0d-D6PE046x.js';
const src = fs.readFileSync(file, 'utf8');

const offsets = [202565, 217559, 218276, 218586, 199029];

for (const o of offsets) {
  const start = Math.max(0, o - 600);
  const end = Math.min(src.length, o + 1200);
  console.log('\n===== OFFSET', o, '=====');
  console.log(src.slice(start, end));
}
