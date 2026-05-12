const fs = require('fs');

const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
const states = oracle.calls;

const keyTriples = [
  ['EO8fB2AQIKXZ5A/qaoglOT88IrBPN9r8lRNmm+KEUzI=', 'hGD3WVRsARKGT1Sx9JF9+E3IHOGwOIpssqTtWArFoO4='],
  ['Ln8y/7k8kWdMHrULDE9x/aalNWbCK+/vC/8gAihXlAQ=', 'iLirVhvDSgvOgxahVeFYx70TnBt0gOtsaQRjPlj5EH8='],
  ['IkY+JZt8Zh4iUvPLDGGztNncx0f4i+VyCfk8b5vY4P0=', 'eICYaqic3pAk1ThfI33wRMxn8IXxyy8DXHfWOx5EGHY='],
  ['k80C/WNNoQeupQlmMdyc60+3WQPiJYY+PRy4Ca3jew8=', 'v/CWoFcLje+WM+9vRvWkkBtvvMTtYOAVejBf3+b+cJc='],
  ['aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=', 'vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w='],
].map(([a, b]) => ({ a: [...Buffer.from(a, 'base64')], b: [...Buffer.from(b, 'base64')] }));

function rc4(key, data) {
  const s = Array.from({ length: 256 }, (_, i) => i);
  let j = 0;
  for (let i = 0; i < 256; i++) {
	j = (j + s[i] + key[i % key.length]) & 255;
	[s[i], s[j]] = [s[j], s[i]];
  }

  const out = [];
  let x = 0;
  let y = 0;
  for (let i = 0; i < data.length; i++) {
	x = (x + 1) & 255;
	y = (y + s[x]) & 255;
	[s[x], s[y]] = [s[y], s[x]];
	out.push(data[i] ^ s[(s[x] + s[y]) & 255]);
  }

  return out;
}

const fns = {
  id: (b) => b,
  c: (b) => (b + 115) & 255,
  b: (b) => (b - 12 + 256) & 255,
  s: (b) => (b + 143) & 255,
  h: (b) => (b - 42 + 256) & 255,
  k: (b) => (b + 15) & 255,
  _: (b) => (b - 20 + 256) & 255,
  f: (b) => (b - 188 + 256) & 255,
  m: (b) => b ^ 177,
  y: (b) => ((b >>> 1) | (b << 7)) & 255,
  g: (b) => ((b << 2) | (b >>> 6)) & 255,
  $: (b) => ((b << 4) | (b >>> 4)) & 255,
  Y: (b) => ((b << 1) | (b >>> 7)) & 255,
  G: (b) => ((b >>> 2) | (b << 6)) & 255,
};

function deriveTransition(outputState, inputState, rc4Key, xorKey, pc) {
  if (outputState.length - inputState.length !== pc) return null;
  const rc4Out = rc4(rc4Key, inputState);
  const slots = Array.from({ length: 10 }, () => new Set(Object.keys(fns)));

  for (let i = 0; i < inputState.length; i++) {
	const outIdx = i < pc ? (2 * i + 1) : (pc + i);
	const observed = outputState[outIdx];
	const untransformed = rc4Out[i] ^ xorKey[i % xorKey.length];

	const possible = new Set();
	for (const [name, fn] of Object.entries(fns)) {
	  if (fn(untransformed) === observed) {
		possible.add(name);
	  }
	}

	const slot = slots[i % 10];
	for (const cur of [...slot]) {
	  if (!possible.has(cur)) slot.delete(cur);
	}
  }

  const counts = slots.map((s) => s.size);
  const singles = counts.filter((x) => x === 1).length;
  const empty = counts.filter((x) => x === 0).length;

  return {
	singles,
	empty,
	slots: slots.map((s) => [...s]),
  };
}

for (let t = 0; t < states.length - 1; t++) {
  const O = states[t];
  const I = states[t + 1];
  const pc = O.length - I.length;
  let best = null;

  for (let k = 0; k < keyTriples.length; k++) {
	for (const mode of ['ab', 'ba']) {
	  const rc = mode === 'ab' ? keyTriples[k].a : keyTriples[k].b;
	  const xo = mode === 'ab' ? keyTriples[k].b : keyTriples[k].a;
	  const d = deriveTransition(O, I, rc, xo, pc);
	  if (!d) continue;
	  const score = d.singles * 100 - d.empty * 1000;
	  if (!best || score > best.score) {
		best = { t, k, mode, pc, score, ...d };
	  }
	}
  }

  console.log(`T${t} pc=${pc} best k${best.k} ${best.mode} singles=${best.singles} empty=${best.empty}`);
  console.log(best.slots.map((s) => s.join('/')).join(' | '));
}
