const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const E5 = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const states = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/live-decrypt-oracle.json', 'utf8'));
const E4 = states[0];

const kA = [...Buffer.from('aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=', 'base64')];
const kB = [...Buffer.from('vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w=', 'base64')];
const pc = Buffer.from('YUCisHAu3f3E', 'base64').length; // 9

function rc4(key, data) {
  const s = Array.from({ length: 256 }, (_, i) => i);
  let j = 0;
  for (let i = 0; i < 256; i++) {
	j = (j + s[i] + key[i % key.length]) % 256;
	[s[i], s[j]] = [s[j], s[i]];
  }
  const out = [];
  let x = 0, y = 0;
  for (let i = 0; i < data.length; i++) {
	x = (x + 1) % 256;
	y = (y + s[x]) % 256;
	[s[x], s[y]] = [s[y], s[x]];
	out.push(data[i] ^ s[(s[x] + s[y]) % 256]);
  }
  return out;
}

const fnList = {
  c:  (b) => (b + 115) & 255,
  b:  (b) => (b - 12 + 256) & 255,
  s:  (b) => (b + 143) & 255,
  h:  (b) => (b - 42 + 256) & 255,
  k:  (b) => (b + 15) & 255,
  _:  (b) => (b - 20 + 256) & 255,
  f:  (b) => (b - 188 + 256) & 255,
  m:  (b) => b ^ 177,
  y:  (b) => ((b >>> 1) | (b << 7)) & 255,
  g:  (b) => ((b << 2) | (b >>> 6)) & 255,
  $:  (b) => ((b << 4) | (b >>> 4)) & 255,
};

function intersectModel(rc4Key, xorKey, modelName) {
  const rc = rc4(rc4Key, E4);
  const sets = Array.from({ length: 10 }, () => new Set(Object.keys(fnList)));

  for (let i = 0; i < E4.length; i++) {
	const outIdx = i < pc ? (2 * i + 1) : (pc + i);
	if (outIdx >= E5.length) continue;
	const y = E5[outIdx];

	let x;
	const k = xorKey[i % xorKey.length];
	if (modelName === 'T(rc^k)') x = rc[i] ^ k;
	else if (modelName === 'T(rc)^k') x = rc[i];
	else if (modelName === 'T(rc)+k') x = rc[i];
	else if (modelName === 'T(rc-k)') x = (rc[i] - k + 256) & 255;
	else x = rc[i];

	const matches = new Set();
	for (const [name, fn] of Object.entries(fnList)) {
	  let out;
	  if (modelName === 'T(rc)^k') out = fn(x) ^ k;
	  else if (modelName === 'T(rc)+k') out = (fn(x) + k) & 255;
	  else out = fn(x);
	  if (out === y) matches.add(name);
	}

	const slot = sets[i % 10];
	for (const c of [...slot]) if (!matches.has(c)) slot.delete(c);
  }

  return sets.map(s => [...s]);
}

for (const [name, rc4Key, xorKey] of [
  ['AasRc4', kA, kB],
  ['BasRc4', kB, kA],
]) {
  console.log('===', name, '===');
  for (const model of ['T(rc^k)', 'T(rc)^k', 'T(rc)+k', 'T(rc-k)']) {
	const sets = intersectModel(rc4Key, xorKey, model);
	const sizes = sets.map(s => s.length);
	const total = sizes.reduce((a, b) => a + b, 0);
	console.log(model, 'sizes', sizes.join(','), 'total', total, sets.map(s => s.join('/')).join(' | '));
  }
}
