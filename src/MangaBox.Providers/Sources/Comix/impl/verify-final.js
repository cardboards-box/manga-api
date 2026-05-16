'use strict';
// Final verification: re-run the solved constants from ComixToSigner.cs against the fresh corpus
const fs = require('fs');
const corpus = JSON.parse(fs.readFileSync('fresh-corpus.json', 'utf8'));

const PREFIX = [97,200,64,144,162,7,176,70,112,166,46,172,221,0,253,31,196,10,25,32,99,136,29,229,210,13,150,51,132,252,213,72,16,222,85,16,49,197,175,230,100,6,120,233,32,249,167,132,106];
const SUFFIX5 = [127,241,120,206,4,167,103,234,234,27,134];
const SUFFIX4 = [239,59,144,129,223,218,212,83,12,179,59];
const MASKS5 = [1n,35n,17973325n,1332340n,20976474n,17189124n,4610622n,32n,256n,544n,1536n,17515604n,6245121n,6249217n,460377n,17826603n,197748n,16976482n,17957399n,21170754n,17192752n,65568n,196608n,21059884n,18092892n,22942580n,0n,18357311n,1595162n,5381143n,6101306n,1660195n,6248992n,18355534n,16977780n,1461359n,6243885n,6119274n,17700693n,18502400n];
const MASKS4 = [1n,35n,1029n,42n,1083n,1308n,327n,32n,256n,544n,1536n,1136n,592n,512n,1363n,544n,531n,363n,108n,562n,530n,1804n,1363n,1870n,1071n,559n,0n,626n,310n,816n,812n,615n];

function sign(id) {
  const isShort = id.length <= 4;
  const masks = isShort ? MASKS4 : MASKS5;
  const suffix = isShort ? SUFFIX4 : SUFFIX5;
  const varStart = 49, varCount = isShort ? 4 : 5;

  const bytes = [...PREFIX, ...new Array(varCount).fill(0), ...suffix];
  const charCount = isShort ? 4 : 5;

  // Build feature bits from ID chars
  const fb = BigInt('0x' + [...Array(charCount)].map((_,i) => {
	const ch = i < id.length ? id.charCodeAt(i) : 0;
	return ch.toString(16).padStart(2,'0');
  }).join(''));

  // Actually, build as LSB-first per char
  let featureBits = 0n;
  let bitIdx = 0;
  for (let i = 0; i < charCount; i++) {
	const ch = BigInt(i < id.length ? id.charCodeAt(i) : 0);
	for (let b = 0; b < 8; b++) {
	  if ((ch >> BigInt(b)) & 1n) featureBits |= 1n << BigInt(bitIdx);
	  bitIdx++;
	}
  }

  for (let ob = 0; ob < masks.length; ob++) {
	const v = featureBits & masks[ob];
	let parity = 0n;
	let tmp = v;
	while (tmp) { parity ^= tmp & 1n; tmp >>= 1n; }
	if (parity) {
	  const byteIdx = varStart + (ob >> 3);
	  bytes[byteIdx] |= (1 << (ob & 7));
	}
  }

  return Buffer.from(bytes).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
}

let pass = 0, fail = 0;
for (const { id, token } of corpus) {
  const got = sign(id);
  if (got === token) pass++;
  else { fail++; console.log('FAIL', id, '\n  exp:', token, '\n  got:', got); }
}
console.log(`\nResult: ${pass}/${corpus.length} pass, ${fail} fail`);
