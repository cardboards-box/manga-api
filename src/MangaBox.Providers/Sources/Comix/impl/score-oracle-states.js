const fs = require('fs');

const o = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
const states = o.calls;

const keys = [
  ['EO8fB2AQIKXZ5A/qaoglOT88IrBPN9r8lRNmm+KEUzI=', 'hGD3WVRsARKGT1Sx9JF9+E3IHOGwOIpssqTtWArFoO4=', 'jUctkam5GFGxUA=='],
  ['Ln8y/7k8kWdMHrULDE9x/aalNWbCK+/vC/8gAihXlAQ=', 'iLirVhvDSgvOgxahVeFYx70TnBt0gOtsaQRjPlj5EH8=', 'bcbQp+o6'],
  ['IkY+JZt8Zh4iUvPLDGGztNncx0f4i+VyCfk8b5vY4P0=', 'eICYaqic3pAk1ThfI33wRMxn8IXxyy8DXHfWOx5EGHY=', 'Gi+iYUq9'],
  ['k80C/WNNoQeupQlmMdyc60+3WQPiJYY+PRy4Ca3jew8=', 'v/CWoFcLje+WM+9vRvWkkBtvvMTtYOAVejBf3+b+cJc=', 'eBRPAsbPDw=='],
  ['aUvDZX3P3oZ53+JPe68doZCPPyTlX2I8LNmQU9dew7U=', 'vCN7sFSIzrrs1lZ7cC3bWQldvHXNWPocVLAvgwgUs1w=', 'YUCisHAu3f3E'],
].map(([a, b, p]) => ({ a: [...Buffer.from(a, 'base64')], b: [...Buffer.from(b, 'base64')], pc: Buffer.from(p, 'base64').length }));

function rc4(key, data) {
  const s = Array.from({ length: 256 }, (_, i) => i);
  let j = 0;
  for (let i = 0; i < 256; i++) {
	j = (j + s[i] + key[i % key.length]) & 255;
	[s[i], s[j]] = [s[j], s[i]];
  }

  let x = 0;
  let y = 0;
  const out = [];
  for (let i = 0; i < data.length; i++) {
	x = (x + 1) & 255;
	y = (y + s[x]) & 255;
	[s[x], s[y]] = [s[y], s[x]];
	out.push(data[i] ^ s[(s[x] + s[y]) & 255]);
  }

  return out;
}

function unweave(output, pc) {
  const inputLen = output.length - pc;
  const arr = new Array(inputLen);
  let outIdx = 0;
  for (let i = 0; i < inputLen; i++) {
	if (i < pc) outIdx++;
	arr[i] = output[outIdx++];
  }
  return arr;
}

function reverseIdentityTransform(output, rc4Key, xorKey, pc) {
  const uw = unweave(output, pc);
  const rc4Out = uw.map((v, i) => v ^ xorKey[i % xorKey.length]);
  return rc4(rc4Key, rc4Out);
}

function score(a, b) {
  let s = 0;
  const n = Math.min(a.length, b.length);
  for (let i = 0; i < n; i++) {
	if (a[i] === b[i]) s++;
  }
  return s;
}

for (let si = 0; si < states.length - 1; si++) {
  const O = states[si];
  const I = states[si + 1];
  let best = { s: -1, desc: '' };

  for (let ki = 0; ki < keys.length; ki++) {
	for (const mode of ['ab', 'ba']) {
	  const rc = mode === 'ab' ? keys[ki].a : keys[ki].b;
	  const xo = mode === 'ab' ? keys[ki].b : keys[ki].a;
	  const r = reverseIdentityTransform(O, rc, xo, keys[ki].pc);
	  const sc = score(r, I);
	  if (sc > best.s) {
		best = {
		  s: sc,
		  desc: `state${si}->${si + 1} k${ki} ${mode} pc${keys[ki].pc}`,
		};
	  }
	}
  }

  console.log(best.desc, 'score', best.s, 'of', I.length);
}
