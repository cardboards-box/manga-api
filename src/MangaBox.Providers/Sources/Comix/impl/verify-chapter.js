'use strict';
const PREFIX = Buffer.from([
  97, 200, 64, 144, 162, 7, 176, 70, 112, 166, 46, 172, 221, 0, 253, 31, 196, 10, 25, 32,
  99, 136, 29, 229, 210, 13, 150, 51, 132, 252, 213, 72, 16, 222, 85, 208, 49, 100, 175, 133,
  100, 39, 120, 162, 32, 241, 167, 252, 185, 73, 100, 243,
]);
const TABLE = [
	0,   0,   0,   0,   0,   0,   0,   0, 107,  11,
   85, 117, 149, 181,   0,   0,   0,   0,  78, 110,
	0, 120,  59,   0, 187, 123,  58, 251, 190, 126,
	1,   0,   0, 249,  33,   0,  17,  25, 193, 201,
   15,  55,  31,   7, 232,  23, 248, 224, 200, 240,
  228, 132,  36,   0, 100,   4, 164,  68, 229, 133,
   46, 238, 175, 110,   0, 233, 174, 105,  40, 232,
];
const VAR_START = 52, VAR_COUNT = 7;

function sign(chapterId) {
  const bytes = Buffer.alloc(VAR_START + VAR_COUNT);
  PREFIX.copy(bytes);
  for (let p = 0; p < VAR_COUNT && p < chapterId.length; p++) {
	const ci = chapterId.charCodeAt(p) - 48;
	if (ci >= 0 && ci <= 9) bytes[VAR_START + p] = TABLE[p * 10 + ci];
  }
  return bytes.toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
}

console.log('9343749:', sign('9343749'));
console.log('8919603:', sign('8919603'));

const corpus = require('./chapter-corpus.json');
let pass = 0, fail = 0;
for (const {chapterId, token} of corpus) {
  const got = sign(chapterId);
  if (got === token) pass++;
  else { fail++; console.log('FAIL', chapterId, '|', got.slice(-15), 'vs', token.slice(-15)); }
}
console.log('Corpus:', pass + '/' + corpus.length, 'pass');
