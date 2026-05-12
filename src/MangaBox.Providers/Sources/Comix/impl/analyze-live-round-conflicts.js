const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const s0 = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/live-decrypt-oracle.json', 'utf8'));
const states = [s0, ...oracle.slice(0, 5)]; // decryption pipeline only

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

for (let step = 0; step < 5; step++) {
  const logicalRound = 5 - step;
  const tri = triples[logicalRound - 1];
  const O = states[step];
  const I = states[step + 1];

  for (const [role, rc4Key, xorKey] of [['A', tri.a, tri.b], ['B', tri.b, tri.a]]) {
	const rc = rc4(rc4Key, I);

	for (const idxMode of ['interleave', 'prefix']) {
	  let conflicts = 0;
	  let samples = 0;
	  const maps = Array.from({ length: 10 }, () => new Map());

	  for (let i = 0; i < I.length; i++) {
		const outIdx = idxMode === 'interleave'
		  ? (i < tri.pc ? (2 * i + 1) : (tri.pc + i))
		  : (tri.pc + i);

		if (outIdx >= O.length) continue;

		const y = O[outIdx];
		const x = rc[i] ^ xorKey[i % xorKey.length];
		const posMap = maps[i % 10];

		if (!posMap.has(y)) {
		  posMap.set(y, x);
		} else if (posMap.get(y) !== x) {
		  conflicts++;
		}

		samples++;
	  }

	  console.log(`step ${step + 1} round ${logicalRound} role ${role} ${idxMode} pc=${tri.pc} conflicts=${conflicts} samples=${samples}`);
	}
  }
}
