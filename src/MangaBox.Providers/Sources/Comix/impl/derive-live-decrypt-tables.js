const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const encrypted = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/live-decrypt-oracle.json', 'utf8'));

const states = [encrypted, ...oracle.slice(0, 5)]; // S0..S5

const triples = [
  ['EO8fB2AQIKXZ5A/qaoglOT88IrBPN9r8lRNmm+KEUzI=', 'hGD3WVRsARKGT1Sx9JF9+E3IHOGwOIpssqTtWArFoO4=', 'jUctkam5GFGxUA=='],
  ['Ln8y/7k8kWdMHrULDE9x/aalNWbCK+/vC/8gAihXlAQ=', 'iLirVhvDSgvOgxahVeFYx70TnBt0gOtsaQRjPlj5EH8=', 'bcbQp+o6'],
  ['IkY+JZt8Zh4iUvPLDGGztNncx0f4i+VyCfk8b5vY4P0=', 'eICYaqic3pAk1ThfI33wRMxn8IXxyy8DXHfWOx5EGHY=', 'Gi+iYUq9'],
  ['k80C/WNNoQeupQlmMdyc60+3WQPiJYY+PRy4Ca3jew8=', 'v/CWoFcLje+WM+9vRvWkkBtvvMTtYOAVejBf3+b+cJc=', 'eBRPAsbPDw=='],
  ['aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=', 'vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w=', 'YUCisHAu3f3E']
].map(([a, b, p]) => ({ a: [...Buffer.from(a, 'base64')], b: [...Buffer.from(b, 'base64')], pc: Buffer.from(p, 'base64').length }));

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

const fns = {
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

function derive(roundIndex, rc4Key, xorKey, pc) {
  const O = states[roundIndex];
  const I = states[roundIndex + 1];

  const rc4Out = rc4(rc4Key, I);
  const candidates = Array.from({ length: 10 }, () => new Set(Object.keys(fns)));

  for (let i = 0; i < I.length; i++) {
	const outIdx = i < pc ? (2 * i + 1) : (pc + i);
	const transformed = O[outIdx];
	const untransformed = rc4Out[i] ^ xorKey[i % xorKey.length];

	const matches = new Set();
	for (const [name, fn] of Object.entries(fns)) {
	  if (fn(untransformed) === transformed) matches.add(name);
	}

	const slot = candidates[i % 10];
	for (const cur of [...slot]) {
	  if (!matches.has(cur)) slot.delete(cur);
	}
  }

  return candidates.map(s => [...s]);
}

for (let k = 0; k < 5; k++) {
  const logicalRound = 5 - k;
  const tri = triples[logicalRound - 1];
  const ca = derive(k, tri.a, tri.b, tri.pc);
  const cb = derive(k, tri.b, tri.a, tri.pc);

  const scoreA = ca.reduce((n, s) => n + (s.length === 1 ? 1 : 0), 0);
  const scoreB = cb.reduce((n, s) => n + (s.length === 1 ? 1 : 0), 0);

  console.log(`Step ${k + 1} (round ${logicalRound}, pc=${tri.pc})`);
  console.log(' A-as-rc4 score', scoreA, ca.map(s => s.join('/')).join(' | '));
  console.log(' B-as-rc4 score', scoreB, cb.map(s => s.join('/')).join(' | '));
}
