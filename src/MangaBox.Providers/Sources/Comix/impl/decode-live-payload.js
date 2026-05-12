const source = process.argv[2];

if (!source) {
  console.error('Usage: node decode-live-payload.js <base64url-payload | @file-with-payload>');
  process.exit(1);
}

const fs = require('fs');
const payload = source.startsWith('@') ? fs.readFileSync(source.slice(1), 'utf8').trim() : source;

const rc4Keys = [
  '13YDu67uDgFczo3DnuTIURqas4lfMEPADY6Jaeqky+w=',
  'vZ23RT7pbSlxwiygkHd1dhToIku8SNHPC6V36L4cnwM=',
  'BkWI8feqSlDZKMq6awfzWlUypl88nz65KVRmpH0RWIc=',
  'RougjiFHkSKs20DZ6BWXiWwQUGZXtseZIyQWKz5eG34=',
  'U9LRYFL2zXU4TtALIYDj+lCATRk/EJtH7/y7qYYNlh8=',
].map(k => [...Buffer.from(k, 'base64')]);

const xorKeys = [
  'yEy7wBfBc+gsYPiQL/4Dfd0pIBZFzMwrtlRQGwMXy3Q=',
  'QX0sLahOByWLcWGnv6l98vQudWqdRI3DOXBdit9bxCE=',
  'v7EIpiQQjd2BGuJzMbBA0qPWDSS+wTJRQ7uGzZ6rJKs=',
  'LL97cwoDoG5cw8QmhI+KSWzfW+8VehIh+inTxnVJ2ps=',
  'e/GtffFDTvnw7LBRixAD+iGixjqTq9kIZ1m0Hj+s6fY=',
].map(k => [...Buffer.from(k, 'base64')]);

const prependCounts = [7, 6, 7, 8, 6];

function decodeBase64Url(s) {
  const b = s.replace(/-/g, '+').replace(/_/g, '/');
  const p = b + '==='.slice((b.length + 3) % 4);
  return [...Buffer.from(p, 'base64')];
}

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

function ror1(v) { return ((v << 1) | (v >>> 7)) & 255; }
function rol4(v) { return ((v << 4) | (v >>> 4)) & 255; }
function rol2(v) { return ((v >>> 2) | (v << 6)) & 255; }

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

function reverseRound(round, output) {
  const pc = prependCounts[round];
  if (output.length < pc) return output;

  const inputLength = output.length - pc;
  const rc4Out = new Array(inputLength);
  let outIdx = 0;

  for (let i = 0; i < inputLength; i++) {
	if (i < pc) outIdx++;
	if (outIdx >= output.length) break;

	const transformed = output[outIdx++];
	const untransformed = reverseTransform(round, i % 10, transformed);
	rc4Out[i] = untransformed ^ xorKeys[round][i % 32];
  }

  return rc4(rc4Keys[round], rc4Out);
}

const raw = decodeBase64Url(payload);
console.log('input length', raw.length);
let bytes = raw;
for (let round = 4; round >= 0; round--) {
  bytes = reverseRound(round, bytes);
}

const out = Buffer.from(bytes);
console.log('out length', out.length);
console.log('utf8 head', out.subarray(0, 120).toString('utf8'));
console.log('hex head', out.subarray(0, 64).toString('hex'));
