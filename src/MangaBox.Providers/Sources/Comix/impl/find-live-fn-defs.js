const fs = require('fs');

const src = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/secure-teup0d-D6PE046x.js', 'utf8');

for (const name of ['mR', 'm7', 'gt', 'ge']) {
  const patterns = [
	`function ${name}(`,
	`${name}=function(`,
	`let ${name}=function(`,
	`var ${name}=function(`,
	`const ${name}=function(`,
	`let ${name} = function(`,
	`var ${name} = function(`,
	`const ${name} = function(`,
  ];

  let found = false;
  for (const p of patterns) {
	const idx = src.indexOf(p);
	if (idx >= 0) {
	  found = true;
	  console.log('\n===', name, 'pattern', p, 'idx', idx, '===');
	  console.log(src.slice(Math.max(0, idx - 250), Math.min(src.length, idx + 900)));
	  break;
	}
  }

  if (!found) {
	console.log(name, 'not found');
  }
}
