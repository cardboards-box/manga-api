const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const s0 = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const rOracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
const states = [s0, ...rOracle.calls.slice(0, 5)]; // 6 states (input + 5 stage outputs)

const triples = [
  ['EO8fB2AQIKXZ5A/qaoglOT88IrBPN9r8lRNmm+KEUzI=', 'hGD3WVRsARKGT1Sx9JF9+E3IHOGwOIpssqTtWArFoO4=', 'jUctkam5GFGxUA=='], // pc10
  ['Ln8y/7k8kWdMHrULDE9x/aalNWbCK+/vC/8gAihXlAQ=', 'iLirVhvDSgvOgxahVeFYx70TnBt0gOtsaQRjPlj5EH8=', 'bcbQp+o6'], // pc6
  ['IkY+JZt8Zh4iUvPLDGGztNncx0f4i+VyCfk8b5vY4P0=', 'eICYaqic3pAk1ThfI33wRMxn8IXxyy8DXHfWOx5EGHY=', 'Gi+iYUq9'], // pc6
  ['k80C/WNNoQeupQlmMdyc60+3WQPiJYY+PRy4Ca3jew8=', 'v/CWoFcLje+WM+9vRvWkkBtvvMTtYOAVejBf3+b+cJc=', 'eBRPAsbPDw=='], // pc7
  ['aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=', 'vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w=', 'YUCisHAu3f3E'], // pc9
].map(([a, b, p], i) => ({ idx: i, a: [...Buffer.from(a, 'base64')], b: [...Buffer.from(b, 'base64')], pc: Buffer.from(p, 'base64').length }));

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

// Step diffs: 9,7,6,6,10 => fixed mapping except the two pc=6 stages.
const fixed = [
  triples.find(t => t.pc === 9),
  triples.find(t => t.pc === 7),
  null,
  null,
  triples.find(t => t.pc === 10),
];

const sixes = triples.filter(t => t.pc === 6);

function analyze(stageTriples, roleMask, stageModes) {
  let totalConflicts = 0;
  let totalSamples = 0;

  for (let step = 0; step < 5; step++) {
	const O = states[step];
	const I = states[step + 1];
	const t = stageTriples[step];
	const useA = ((roleMask >> step) & 1) === 1;
	const rc4Key = useA ? t.a : t.b;
	const xorKey = useA ? t.b : t.a;

	const cfg = stageModes[step];
	const rc = rc4(rc4Key, I);

	const maps = Array.from({ length: cfg.mod }, () => new Map());
	let conflicts = 0;

	for (let i = 0; i < I.length; i++) {
	  const outIdx = cfg.mode === 'interleave'
		? (i < t.pc ? (2 * i + 1) : (t.pc + i))
		: (t.pc + i);
	  if (outIdx >= O.length) continue;

	  const y = O[outIdx];
	  const x = rc[i] ^ xorKey[i % xorKey.length];
	  const m = maps[i % cfg.mod];

	  if (!m.has(x)) {
		m.set(x, y);
	  } else if (m.get(x) !== y) {
		conflicts++;
	  }
	  totalSamples++;
	}

	totalConflicts += conflicts;
  }

  return { totalConflicts, totalSamples };
}

const stageAssignments = [
  [fixed[0], fixed[1], sixes[0], sixes[1], fixed[4]],
  [fixed[0], fixed[1], sixes[1], sixes[0], fixed[4]],
];

let best = { conflicts: Number.MAX_SAFE_INTEGER };

for (const assign of stageAssignments) {
  for (let roleMask = 0; roleMask < 32; roleMask++) {
	// Greedy pick best mode/mod per stage independently.
	const chosen = [];
	let conflicts = 0;

	for (let step = 0; step < 5; step++) {
	  let bestLocal = { conflicts: Number.MAX_SAFE_INTEGER };
	  for (const mode of ['interleave', 'prefix']) {
		for (let mod = 1; mod <= 32; mod++) {
		  const stageModes = [
			{ mode: 'interleave', mod: 10 },
			{ mode: 'interleave', mod: 10 },
			{ mode: 'interleave', mod: 10 },
			{ mode: 'interleave', mod: 10 },
			{ mode: 'interleave', mod: 10 },
		  ];
		  stageModes[step] = { mode, mod };

		  const a = analyze(assign, roleMask, stageModes);
		  // isolate only current stage contribution by running single-stage equivalent
		  const O = states[step];
		  const I = states[step + 1];
		  const t = assign[step];
		  const useA = ((roleMask >> step) & 1) === 1;
		  const rc4Key = useA ? t.a : t.b;
		  const xorKey = useA ? t.b : t.a;
		  const rc = rc4(rc4Key, I);
		  const maps = Array.from({ length: mod }, () => new Map());
		  let c = 0;
		  for (let i = 0; i < I.length; i++) {
			const outIdx = mode === 'interleave' ? (i < t.pc ? (2 * i + 1) : (t.pc + i)) : (t.pc + i);
			if (outIdx >= O.length) continue;
			const y = O[outIdx];
			const x = rc[i] ^ xorKey[i % xorKey.length];
			const m = maps[i % mod];
			if (!m.has(x)) m.set(x, y);
			else if (m.get(x) !== y) c++;
		  }
		  if (c < bestLocal.conflicts) bestLocal = { conflicts: c, mode, mod };
		}
	  }

	  chosen.push(bestLocal);
	  conflicts += bestLocal.conflicts;
	}

	if (conflicts < best.conflicts) {
	  best = {
		conflicts,
		assign: assign.map(t => ({ idx: t.idx, pc: t.pc })),
		roleMask,
		chosen,
	  };
	  console.log('BEST', JSON.stringify(best));
	}
  }
}

console.log('FINAL', JSON.stringify(best));
