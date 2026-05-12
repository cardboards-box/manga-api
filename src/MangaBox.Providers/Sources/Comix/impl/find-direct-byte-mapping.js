const fs = require('fs');

const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
const payload = oracle.e;
const s0 = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const states = [s0, ...oracle.calls.slice(0, 5)];
const pcs = [9,7,6,6,10]; // observed per step from length deltas

function evalStage(step, mode, mod, useBucket2=false) {
  const O = states[step];
  const I = states[step+1];
  const pc = pcs[step];

  const maps = new Map();
  let conflicts = 0;
  let samples = 0;

  for (let i=0;i<I.length;i++) {
	const outIdx = mode==='interleave' ? (i < pc ? (2*i+1) : (pc+i)) : (pc+i);
	if (outIdx >= O.length) continue;

	const inByte = O[outIdx];
	const outByte = I[i];

	const bucket = useBucket2 ? (i < pc ? 0 : 1) : 0;
	const cls = i % mod;
	const key = `${bucket}|${cls}|${inByte}`;

	if (!maps.has(key)) maps.set(key, outByte);
	else if (maps.get(key) !== outByte) conflicts++;

	samples++;
  }

  return { conflicts, samples, rules: maps.size };
}

for (let step=0; step<5; step++) {
  let best = null;
  for (const mode of ['interleave','prefix']) {
	for (const bucket of [false,true]) {
	  for (let mod=1; mod<=128; mod++) {
		const r = evalStage(step, mode, mod, bucket);
		if (!best || r.conflicts < best.conflicts || (r.conflicts===best.conflicts && r.rules<best.rules)) {
		  best = { step: step+1, mode, bucket, mod, ...r };
		}
	  }
	}
  }
  console.log('BEST_STAGE', best);
}
