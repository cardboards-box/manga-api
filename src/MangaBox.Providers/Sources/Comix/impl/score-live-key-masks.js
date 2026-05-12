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

for (let mask = 0; mask < 32; mask++) {
  let conflicts = 0;
  let mappings = 0;

  for (let step = 0; step < 5; step++) {
	const logicalRound = 5 - step;
	const tri = triples[logicalRound - 1];
	const rc4Key = ((mask >> (logicalRound - 1)) & 1) ? tri.a : tri.b;
	const xorKey = ((mask >> (logicalRound - 1)) & 1) ? tri.b : tri.a;

	const O = states[step];
	const I = states[step + 1];
	const pc = tri.pc;

	const rc4Out = rc4(rc4Key, I);

	const mapsByPos = Array.from({ length: 10 }, () => new Map());

	for (let i = 0; i < I.length; i++) {
	  const outIdx = i < pc ? (2 * i + 1) : (pc + i);
	  if (outIdx >= O.length) continue;

	  const transformed = O[outIdx];
	  const untransformed = rc4Out[i] ^ xorKey[i % xorKey.length];

	  const m = mapsByPos[i % 10];
	  if (!m.has(transformed)) {
		m.set(transformed, untransformed);
		mappings++;
	  } else if (m.get(transformed) !== untransformed) {
		conflicts++;
	  }
	}
  }

  console.log('mask', mask.toString(2).padStart(5, '0'), 'conflicts', conflicts, 'mappings', mappings);
}
