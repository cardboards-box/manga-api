const fs = require('fs');

const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
const payload = oracle.e;
const s0 = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const states = [s0, ...oracle.calls.slice(0, 5)];

const triples = [
  ['EO8fB2AQIKXZ5A/qaoglOT88IrBPN9r8lRNmm+KEUzI=', 'hGD3WVRsARKGT1Sx9JF9+E3IHOGwOIpssqTtWArFoO4=', 'jUctkam5GFGxUA=='],
  ['Ln8y/7k8kWdMHrULDE9x/aalNWbCK+/vC/8gAihXlAQ=', 'iLirVhvDSgvOgxahVeFYx70TnBt0gOtsaQRjPlj5EH8=', 'bcbQp+o6'],
  ['IkY+JZt8Zh4iUvPLDGGztNncx0f4i+VyCfk8b5vY4P0=', 'eICYaqic3pAk1ThfI33wRMxn8IXxyy8DXHfWOx5EGHY=', 'Gi+iYUq9'],
  ['k80C/WNNoQeupQlmMdyc60+3WQPiJYY+PRy4Ca3jew8=', 'v/CWoFcLje+WM+9vRvWkkBtvvMTtYOAVejBf3+b+cJc=', 'eBRPAsbPDw=='],
  ['aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=', 'vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w=', 'YUCisHAu3f3E'],
].map(([a,b,p],idx)=>({idx,a:[...Buffer.from(a,'base64')],b:[...Buffer.from(b,'base64')],pc:Buffer.from(p,'base64').length}));

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

function* perms(arr) {
  if (arr.length <= 1) { yield arr.slice(); return; }
  for (let i = 0; i < arr.length; i++) {
	const first = arr[i];
	const rest = arr.slice(0, i).concat(arr.slice(i + 1));
	for (const p of perms(rest)) yield [first, ...p];
  }
}

let best = { conflicts: Number.MAX_SAFE_INTEGER };

for (const order of perms(triples)) {
  for (let roleMask = 0; roleMask < 32; roleMask++) {
	let conflicts = 0;

	for (let step = 0; step < 5; step++) {
	  const O = states[step];
	  const I = states[step + 1];
	  const t = order[step]; // step order is direct: s0->s1->... (round order already)

	  const useA = ((roleMask >> step) & 1) === 1;
	  const rc4Key = useA ? t.a : t.b;
	  const xorKey = useA ? t.b : t.a;

	  const rc = rc4(rc4Key, I);
	  const maps = Array.from({ length: 10 }, () => new Map());

	  for (let i = 0; i < I.length; i++) {
		const outIdx = i < t.pc ? (2 * i + 1) : (t.pc + i);
		if (outIdx >= O.length) { conflicts += 1000; break; }

		const y = O[outIdx];
		const x = rc[i] ^ xorKey[i % xorKey.length];

		const m = maps[i % 10];
		if (!m.has(x)) m.set(x, y);
		else if (m.get(x) !== y) conflicts++;
	  }
	}

	if (conflicts < best.conflicts) {
	  best = {
		conflicts,
		order: order.map(t => ({ idx: t.idx, pc: t.pc })),
		roleMask,
	  };
	  console.log('BEST', JSON.stringify(best));
	}
  }
}

console.log('FINAL', JSON.stringify(best));
