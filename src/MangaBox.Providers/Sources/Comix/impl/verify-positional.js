'use strict';
// Final verification: reproduce C# ComixToSigner logic in JS and test all corpus entries
const fs = require('fs');
const all = require('./merged-corpus.json');
// Also add 8w6dm
const cap = require('./capture-8w6dm.json');
if (cap.events && cap.events[0]) {
  const ev = cap.events[0];
  const tok = ev.result.replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
  if (!all.find(e => e.id === '8w6dm')) all.push({ id: '8w6dm', bytes: ev.bytes, token: tok });
}

const PREFIX = [97,200,64,144,162,7,176,70,112,166,46,172,221,0,253,31,196,10,25,32,99,136,29,229,210,13,150,51,132,252,213,72,16,222,85,16,49,197,175,230,100,6,120,233,32,249,167,132,106];
const SUFFIX5 = [127,241,120,206,4,167,103,234,234,27,134];
const SUFFIX4 = [239,59,144,129,223,218,212,83,12,179,59];
const T5 = [158,157,152,135,0,153,164,163,166,165,0,0,0,138,137,0,211,0,0,208,223,210,209,220,0,0,221,216,0,0,0,228,227,230,229,224,106,107,100,0,0,111,104,105,146,147,0,0,0,62,63,0,57,0,0,0,61,6,7,32,0,0,43,36,0,0,0,40,41,82,0,44,167,199,105,137,0,72,233,9,168,200,0,0,0,46,78,0,15,0,0,116,148,47,79,244,0,0,207,113,0,0,0,0,17,176,208,110,50,82,114,146,0,210,43,203,107,11,0,0,0,217,120,0,185,0,0,153,57,218,121,26,0,0,250,154,0,0,0,0,179,83,243,147,85,117,149,181,0,0,21,53,78,110,0,0,0,199,231,0,39,0,0,136,168,200,232,8,0,0,109,141,0,0,0,13,0,70,102,134];
const T4 = [158,157,0,135,0,153,0,163,166,165,0,0,0,0,137,0,0,0,0,208,223,210,209,220,0,0,221,216,0,0,0,0,0,0,0,0,0,107,100,101,0,0,104,105,146,147,0,0,0,0,63,0,0,0,0,0,61,0,7,32,0,0,0,36,0,0,0,40,41,0,83,0,0,0,105,137,0,0,233,9,168,0,0,0,0,0,0,0,0,0,0,116,148,0,79,0,0,0,0,113,0,0,0,0,17,0,208,0,50,82,0,146,0,210,0,0,107,0,0,0,0,217,120,0,185,0,0,153,0,218,121,0,0,0,0,154,0,0,0,19,0,0,0,147];

function charToIdx(c) {
  if (c >= '0' && c <= '9') return c.charCodeAt(0) - 48;
  if (c >= 'a' && c <= 'z') return c.charCodeAt(0) - 87;
  return -1;
}
function sign(id) {
  const isShort = id.length <= 4;
  const table = isShort ? T4 : T5;
  const varCount = isShort ? 4 : 5;
  const suffix = isShort ? SUFFIX4 : SUFFIX5;
  const bytes = [...PREFIX, ...new Array(varCount).fill(0), ...suffix];
  for (let p = 0; p < varCount && p < id.length; p++) {
	const ci = charToIdx(id[p]);
	if (ci >= 0) bytes[49 + p] = table[p * 36 + ci];
  }
  return Buffer.from(bytes).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
}

let pass = 0, fail = 0, skip = 0;
for (const { id, token } of all) {
  const got = sign(id);
  if (got === token) pass++;
  else { fail++; console.log(`FAIL ${id}: exp=${token.slice(56)} got=${got.slice(56)}`); }
}
console.log(`\nResult: ${pass}/${all.length} pass, ${fail} fail`);

// Verify 8w6dm specifically
const t8 = sign('8w6dm');
console.log('\n8w6dm:', t8);
console.log('Browser:', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
console.log('Match:', t8 === 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
