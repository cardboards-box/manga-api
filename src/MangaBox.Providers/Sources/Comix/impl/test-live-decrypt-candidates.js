const fs = require('fs');

const payload = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/payload-e.txt', 'utf8').trim();
const raw = [...Buffer.from((payload.replace(/-/g, '+').replace(/_/g, '/')) + '==='.slice((payload.length + 3) % 4), 'base64')];

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
];

const decoded = liveAtob.map(s => [...Buffer.from(s, 'base64')]);

function rc4(key, data) {
  const S = Array.from({ length: 256 }, (_, i) => i);
  let j = 0;
  for (let i = 0; i < 256; i++) {
	j = (j + S[i] + key[i % key.length]) % 256;
	[S[i], S[j]] = [S[j], S[i]];
  }
  const out = [];
  let x = 0; let y = 0;
  for (let i = 0; i < data.length; i++) {
	x = (x + 1) % 256;
	y = (y + S[x]) % 256;
	[S[x], S[y]] = [S[y], S[x]];
	out.push(data[i] ^ S[(S[x] + S[y]) % 256]);
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
		case 0:
		case 9: return (v - 115 + 256) & 255;
		case 1: return (v + 12) & 255;
		case 2:
		case 8: return ror1(v);
		case 3: return rol4(v);
		case 4:
		case 6: return (v + 42) & 255;
		case 5: return (v - 143 + 256) & 255;
		case 7: return (v - 15 + 256) & 255;
		default: return v;
	  }
	case 1:
	  switch (pos) {
		case 0:
		case 8: return (v - 115 + 256) & 255;
		case 1: return (v + 12) & 255;
		case 2:
		case 6: return rol4(v);
		case 3: return (v + 42) & 255;
		case 4:
		case 9: return (v - 143 + 256) & 255;
		case 5: return (v - 15 + 256) & 255;
		case 7: return (v + 20) & 255;
		default: return v;
	  }
	case 2:
	  switch (pos) {
		case 0: return (v - 115 + 256) & 255;
		case 1: return (v + 188) & 255;
		case 2:
		case 8: return (v - 143 + 256) & 255;
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
		case 1:
		case 9: return v ^ 177;
		case 2:
		case 7:
		case 8: return ror1(v);
		case 3:
		case 5: return (v - 143 + 256) & 255;
		case 4:
		case 6: return (v + 20) & 255;
		default: return v;
	  }
	case 4:
	  switch (pos) {
		case 0: return (v + 20) & 255;
		case 1:
		case 7: return (v - 143 + 256) & 255;
		case 2: return (v - 115 + 256) & 255;
		case 3:
		case 5: return v ^ 177;
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

function decryptWith(rc4Keys, xorKeys, prependCounts) {
  let bytes = raw.slice();
  for (let round = 4; round >= 0; round--) {
	const pc = prependCounts[round];
	if (bytes.length < pc) return null;
	const inputLength = bytes.length - pc;
	const rc4Out = new Array(inputLength);
	let outIdx = 0;
	for (let i = 0; i < inputLength; i++) {
	  if (i < pc) outIdx++;
	  const transformed = bytes[outIdx++];
	  const untransformed = reverseTransform(round, i % 10, transformed);
	  rc4Out[i] = untransformed ^ xorKeys[round][i % xorKeys[round].length];
	}
	bytes = rc4(rc4Keys[round], rc4Out);
  }
  return Buffer.from(bytes);
}

function score(buf) {
  if (!buf) return -1;
  const s = buf.slice(0, 512).toString('utf8');
  let sc = 0;
  if (s.includes('{')) sc += 1;
  if (s.includes('"status"')) sc += 5;
  if (s.includes('"result"')) sc += 5;
  if (s.includes('"chapters"')) sc += 5;
  if (s.startsWith('{') || s.startsWith('[')) sc += 4;
  return sc;
}

const rounds = [
  [decoded[0], decoded[1], decoded[2].length],
  [decoded[3], decoded[4], decoded[5].length],
  [decoded[6], decoded[7], decoded[8].length],
  [decoded[9], decoded[10], decoded[11].length],
  [decoded[12], decoded[13], decoded[14].length],
];

for (const mode of ['A-as-rc4', 'B-as-rc4']) {
  const rc4Keys = rounds.map(r => mode === 'A-as-rc4' ? r[0] : r[1]);
  const xorKeys = rounds.map(r => mode === 'A-as-rc4' ? r[1] : r[0]);
  const prependCounts = rounds.map(r => r[2]);

  const out = decryptWith(rc4Keys, xorKeys, prependCounts);
  const sc = score(out);
  console.log('MODE', mode, 'prepend', prependCounts, 'score', sc, 'outLen', out?.length ?? -1);
  if (out) {
	const latin1 = out.toString('latin1');
	let decoded = '';
	try {
	  decoded = decodeURIComponent(latin1);
	} catch {
	  decoded = '';
	}

	console.log('HEAD_UTF8', out.slice(0, 220).toString('utf8'));
	console.log('HEAD_HEX', out.slice(0, 64).toString('hex'));
	console.log('DECODE_URI_HEAD', decoded.slice(0, 220));
	console.log('DECODE_URI_JSON_HINT', decoded.startsWith('{') && decoded.includes('"status"') && decoded.includes('"result"'));
  }
}
