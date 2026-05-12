const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const s0 = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/live-decrypt-oracle.json', 'utf8'));
const states = [s0, ...oracle.slice(0, 5)]; // step1: s0->s1 is round5 reverse

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

const ops = {
  id: (a, k) => a,
  xor: (a, k) => a ^ k,
  add: (a, k) => (a + k) & 255,
  sub: (a, k) => (a - k + 256) & 255,
  rsub: (a, k) => (k - a + 256) & 255,
};

const idxers = {
  i: (i, len, pc) => i % len,
  iPlusPc: (i, len, pc) => (i + pc) % len,
  iMinusPc: (i, len, pc) => (i - pc + len * 1000) % len,
};

const outModes = {
  interleave: (i, pc) => (i < pc ? 2 * i + 1 : pc + i),
  prefix: (i, pc) => pc + i,
};

function checkRound(step, useAasRc4) {
  const logicalRound = 5 - step;
  const t = triples[logicalRound - 1];
  const O = states[step];
  const I = states[step + 1];

  const rc4Key = useAasRc4 ? t.a : t.b;
  const mixKey = useAasRc4 ? t.b : t.a;
  const rc = rc4(rc4Key, I);

  const results = [];

  for (const [outModeName, outMode] of Object.entries(outModes)) {
	for (const [opName, op] of Object.entries(ops)) {
	  for (const [idxName, idxer] of Object.entries(idxers)) {
		const sets = Array.from({ length: 10 }, () => new Set(Object.keys(fns)));
		let valid = true;

		for (let i = 0; i < I.length; i++) {
		  const outIdx = outMode(i, t.pc);
		  if (outIdx >= O.length) { valid = false; break; }

		  const y = O[outIdx];
		  const k = mixKey[idxer(i, mixKey.length, t.pc)];
		  const x = op(rc[i], k);

		  const matches = new Set();
		  for (const [name, fn] of Object.entries(fns)) {
			if (fn(x) === y) matches.add(name);
		  }

		  const slot = sets[i % 10];
		  for (const c of [...slot]) {
			if (!matches.has(c)) slot.delete(c);
		  }

		  if (slot.size === 0) {
			valid = false;
			break;
		  }
		}

		if (valid) {
		  results.push({ outModeName, opName, idxName, sets: sets.map(s => [...s]) });
		}
	  }
	}
  }

  return results;
}

for (let step = 0; step < 5; step++) {
  console.log('STEP', step + 1, 'round', 5 - step);
  for (const mode of [true, false]) {
	const r = checkRound(step, mode);
	console.log(' ', mode ? 'A-as-rc4' : 'B-as-rc4', 'candidates', r.length);
	for (const cand of r.slice(0, 5)) {
	  console.log('   ', cand.outModeName, cand.opName, cand.idxName, cand.sets.map(s => s.join('/')).join(' | '));
	}
  }
}
