const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const encrypted = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];
const states = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/live-decrypt-oracle.json', 'utf8'));

const liveAtob = [
  'EO8fB2AQIKXZ5A/qaoglOT88IrBPN9r8lRNmm+KEUzI=',
  'hGD3WVRsARKGT1Sx9JF9+E3IHOGwOIpssqTtWArFoO4=',
  'jUctkam5GFGxUA==',
  'Ln8y/7k8kWdMHrULDE9x/aalNWbCK+/vC/8gAihXlAQ=',
  'iLirVhvDSgvOgxahVeFYx70TnBt0gOtsaQRjPlj5EH8=',
  'bcbQp+o6',
  'IkY+JZt8Zh4iUvPLDGGztNncx0f4i+VyCfk8b5vY4P0=',
  'eICYaqic3pAk1ThfI33wRMxn8IXxyy8DXHfWOx5EGHY=',
  'Gi+iYUq9',
  'k80C/WNNoQeupQlmMdyc60+3WQPiJYY+PRy4Ca3jew8=',
  'v/CWoFcLje+WM+9vRvWkkBtvvMTtYOAVejBf3+b+cJc=',
  'eBRPAsbPDw==',
  'aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=',
  'vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w=',
  'YUCisHAu3f3E'
].map(s => [...Buffer.from(s, 'base64')]);

const rounds = [
  { rc4: liveAtob[0], xor: liveAtob[1], pc: liveAtob[2].length },
  { rc4: liveAtob[3], xor: liveAtob[4], pc: liveAtob[5].length },
  { rc4: liveAtob[6], xor: liveAtob[7], pc: liveAtob[8].length },
  { rc4: liveAtob[9], xor: liveAtob[10], pc: liveAtob[11].length },
  { rc4: liveAtob[12], xor: liveAtob[13], pc: liveAtob[14].length },
];

function rc4(key, data) {
  const S = Array.from({ length: 256 }, (_, i) => i);
  let j = 0;
  for (let i = 0; i < 256; i++) {
	j = (j + S[i] + key[i % key.length]) % 256;
	[S[i], S[j]] = [S[j], S[i]];
  }
  const out = [];
  let x = 0;
  let y = 0;
  for (let i = 0; i < data.length; i++) {
	x = (x + 1) % 256;
	y = (y + S[x]) % 256;
	[S[x], S[y]] = [S[y], S[x]];
	out.push(data[i] ^ S[(S[x] + S[y]) % 256]);
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

const revFns = {
  c:  (b) => (b - 115 + 256) & 255,
  b:  (b) => (b + 12) & 255,
  s:  (b) => (b - 143 + 256) & 255,
  h:  (b) => (b + 42) & 255,
  k:  (b) => (b - 15 + 256) & 255,
  _:  (b) => (b + 20) & 255,
  f:  (b) => (b + 188) & 255,
  m:  (b) => b ^ 177,
  y:  (b) => ((b << 1) | (b >>> 7)) & 255,
  g:  (b) => ((b >>> 2) | (b << 6)) & 255,
  $:  (b) => ((b << 4) | (b >>> 4)) & 255,
};

function findMatches(input, expected) {
  return Object.entries(fns).filter(([, fn]) => fn(input) === expected).map(([name]) => name);
}

const dataByRound = [encrypted, ...states.slice(0, 5)];

for (let round = 0; round < 5; round++) {
  const input = dataByRound[round];
  const output = dataByRound[round + 1];
  const pc = rounds[round].pc;

  const rc4Out = rc4(rounds[round].rc4, input);

  const byPos = Array.from({ length: 10 }, () => new Set(Object.keys(fns)));

  for (let i = 0; i < input.length; i++) {
	const xored = rc4Out[i] ^ rounds[round].xor[i % rounds[round].xor.length];

	let outIndex;
	if (i < pc) {
	  outIndex = 2 * i + 1;
	} else {
	  outIndex = pc + i;
	}

	if (outIndex >= output.length) continue;

	const expected = output[outIndex];
	const matches = new Set(findMatches(xored, expected));

	const slot = byPos[i % 10];
	for (const candidate of [...slot]) {
	  if (!matches.has(candidate)) slot.delete(candidate);
	}
  }

  console.log(`Round ${round + 1} pc=${pc}`);
  for (let pos = 0; pos < 10; pos++) {
	const vals = [...byPos[pos]].join('/');
	console.log(`  pos ${pos}: ${vals}`);
  }
}

function reverseRound(round, output, tableByPos) {
  const pc = rounds[round].pc;
  const inputLen = output.length - pc;
  const rc4Out = new Array(inputLen);
  let outIdx = 0;

  for (let i = 0; i < inputLen; i++) {
	if (i < pc) outIdx++;
	const transformed = output[outIdx++];
	const fnName = tableByPos[i % 10];
	const untransformed = revFns[fnName](transformed);
	rc4Out[i] = untransformed ^ rounds[round].xor[i % rounds[round].xor.length];
  }

  return rc4(rounds[round].rc4, rc4Out);
}

const tables = [
  ['c','b','y','$','h','s','h','k','y','c'],
  ['c','b','$','h','s','k','$','_','c','s'],
  ['c','f','s','g','y','m','$','k','s','b'],
  ['b','m','y','s','_','s','_','y','y','m'],
  ['_','s','c','m','b','m','f','s','$','g'],
];

let probe = encrypted.slice();
for (let r = 4; r >= 0; r--) {
  probe = reverseRound(r, probe, tables[r]);
}
const final = Buffer.from(probe);
console.log('final len', final.length);
console.log('final head utf8', final.slice(0, 120).toString('utf8'));
