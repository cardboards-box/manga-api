const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const enc = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];

const pairs = [
  ['EO8fB2AQIKXZ5A/qaoglOT88IrBPN9r8lRNmm+KEUzI=', 'hGD3WVRsARKGT1Sx9JF9+E3IHOGwOIpssqTtWArFoO4=', 'jUctkam5GFGxUA=='],
  ['Ln8y/7k8kWdMHrULDE9x/aalNWbCK+/vC/8gAihXlAQ=', 'iLirVhvDSgvOgxahVeFYx70TnBt0gOtsaQRjPlj5EH8=', 'bcbQp+o6'],
  ['IkY+JZt8Zh4iUvPLDGGztNncx0f4i+VyCfk8b5vY4P0=', 'eICYaqic3pAk1ThfI33wRMxn8IXxyy8DXHfWOx5EGHY=', 'Gi+iYUq9'],
  ['k80C/WNNoQeupQlmMdyc60+3WQPiJYY+PRy4Ca3jew8=', 'v/CWoFcLje+WM+9vRvWkkBtvvMTtYOAVejBf3+b+cJc=', 'eBRPAsbPDw=='],
  ['aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=', 'vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w=', 'YUCisHAu3f3E']
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

const ror1 = (v) => ((v << 1) | (v >>> 7)) & 255;
const rol4 = (v) => ((v << 4) | (v >>> 4)) & 255;
const rol2 = (v) => ((v >>> 2) | (v << 6)) & 255;

function reverseTransform(round, pos, v) {
  switch (round) {
	case 0:
	  switch (pos) {
		case 0: case 9: return (v - 115 + 256) & 255;
		case 1: return (v + 12) & 255;
		case 2: case 8: return ror1(v);
		case 3: return rol4(v);
		case 4: case 6: return (v + 42) & 255;
		case 5: return (v - 143 + 256) & 255;
		case 7: return (v - 15 + 256) & 255;
		default: return v;
	  }
	case 1:
	  switch (pos) {
		case 0: case 8: return (v - 115 + 256) & 255;
		case 1: return (v + 12) & 255;
		case 2: case 6: return rol4(v);
		case 3: return (v + 42) & 255;
		case 4: case 9: return (v - 143 + 256) & 255;
		case 5: return (v - 15 + 256) & 255;
		case 7: return (v + 20) & 255;
		default: return v;
	  }
	case 2:
	  switch (pos) {
		case 0: return (v - 115 + 256) & 255;
		case 1: return (v + 188) & 255;
		case 2: case 8: return (v - 143 + 256) & 255;
		case 3: return rol2(v);
		case 4: return ror1(v);
		case 5: return v ^ 177;
		case 6: return rol4(v);
		case 7: return (v - 15 + 256) & 255;
		case 9: return (v + 12) & 255;
		default: return v;
	  }
	case 3:
	  switch (pos) {
		case 0: return (v + 12) & 255;
		case 1: case 9: return v ^ 177;
		case 2: case 7: case 8: return ror1(v);
		case 3: case 5: return (v - 143 + 256) & 255;
		case 4: case 6: return (v + 20) & 255;
		default: return v;
	  }
	case 4:
	  switch (pos) {
		case 0: return (v + 20) & 255;
		case 1: case 7: return (v - 143 + 256) & 255;
		case 2: return (v - 115 + 256) & 255;
		case 3: case 5: return v ^ 177;
		case 4: return (v + 12) & 255;
		case 6: return (v + 188) & 255;
		case 8: return rol4(v);
		case 9: return rol2(v);
		default: return v;
	  }
	default:
	  return v;
  }
}

function* permutations(arr) {
  if (arr.length <= 1) { yield arr.slice(); return; }
  for (let i = 0; i < arr.length; i++) {
	const first = arr[i];
	const rest = arr.slice(0, i).concat(arr.slice(i + 1));
	for (const perm of permutations(rest)) {
	  yield [first, ...perm];
	}
  }
}

function decrypt(order, roleMask) {
  let bytes = enc.slice();
  for (let r = 4; r >= 0; r--) {
	const pair = order[r];
	const useA = ((roleMask >> r) & 1) === 1;
	const rc4Key = useA ? pair.a : pair.b;
	const xorKey = useA ? pair.b : pair.a;
	const pc = pair.pc;

	const inputLength = bytes.length - pc;
	if (inputLength <= 0) return null;

	const rc4Out = new Array(inputLength);
	let outIdx = 0;
	for (let i = 0; i < inputLength; i++) {
	  if (i < pc) outIdx++;
	  if (outIdx >= bytes.length) return null;

	  const transformed = bytes[outIdx++];
	  const untransformed = reverseTransform(r, i % 10, transformed);
	  rc4Out[i] = untransformed ^ xorKey[i % xorKey.length];
	}

	bytes = rc4(rc4Key, rc4Out);
  }

  return Buffer.from(bytes);
}

let tries = 0;
let best = { score: -1, desc: '' };

for (const order of permutations(pairs)) {
  for (let roleMask = 0; roleMask < 32; roleMask++) {
	tries++;
	const out = decrypt(order, roleMask);
	if (!out) continue;

	const latin1 = out.toString('latin1');
	let dec = '';
	try { dec = decodeURIComponent(latin1); } catch {}

	let score = 0;
	if (latin1.startsWith('%7B%22status%22')) score += 10;
	if (dec.startsWith('{')) score += 5;
	if (dec.includes('"status"')) score += 5;
	if (dec.includes('"result"')) score += 5;
	if (dec.includes('"items"')) score += 5;

	if (score > best.score) {
	  best = {
		score,
		desc: `order=${order.map(p=>p.idx).join(',')} mask=${roleMask.toString(2).padStart(5,'0')}`,
		latinHead: latin1.slice(0, 120),
		decHead: dec.slice(0, 200)
	  };
	  console.log('BEST', best.score, best.desc);
	}

	if (score >= 20) {
	  console.log('FOUND', best);
	  process.exit(0);
	}
  }
}

console.log('done tries', tries, 'best', best);
