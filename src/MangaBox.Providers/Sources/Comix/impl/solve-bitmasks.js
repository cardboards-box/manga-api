// Solve for correct bitmasks using GF(2) linear algebra
// We have oracle pairs and need to find the 40-bit masks

const oracles = [
  ['5r7m', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEapkkCXnvO5CB39rUUwyzOw'],
  ['55kym', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y'],
  ['936j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqVl6ZnvO5CB39rUUwyzOw'],
  ['qqwrm', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat0rEZrof_F4zgSnZ-rqG4Y'],
  ['n93ny', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatyTiRpmf_F4zgSnZ-rqG4Y'],
  ['gxgm9', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatNSD3luf_F4zgSnZ-rqG4Y'],
  ['mr3m0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatEkiXlVf_F4zgSnZ-rqG4Y'],
  ['8v88', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYoqGvvO5CB39rUUwyzOw'],
  ['80d0m', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZqLjLof_F4zgSnZ-rqG4Y'],
  ['nr83', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatwkqJLvO5CB39rUUwyzOw'],
  ['2zxnk', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEapgssBqof_F4zgSnZ-rqG4Y'],
  ['ll172', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGx8uVf_F4zgSnZ-rqG4Y'],
  ['xqz07', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauYrbjI1f_F4zgSnZ-rqG4Y'],
  ['793e', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqOTiXjvO5CB39rUUwyzOw'],
  ['e3jg', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaolldLnvO5CB39rUUwyzOw'],
  ['50l0g', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplqLzInf_F4zgSnZ-rqG4Y'],
  ['0m05n', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap4Hp9IIf_F4zgSnZ-rqG4Y'],
  ['l7re', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatJpcXjvO5CB39rUUwyzOw'],
  ['d0n78', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopq9MtOf_F4zgSnZ-rqG4Y'],
  ['qk31', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat09iVLvO5CB39rUUwyzOw'],
  ['n8we', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatySEXjvO5CB39rUUwyzOw'],
  ['9wz0j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqUpbjKIf_F4zgSnZ-rqG4Y'],
  ['6exl0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqQ_sNpVf_F4zgSnZ-rqG4Y'],
  ['ydq0v', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauU-zzINf_F4zgSnZ-rqG4Y'],
  ['lldl2', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGLtqVf_F4zgSnZ-rqG4Y'],
  ['zdm9j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauA-TwuIf_F4zgSnZ-rqG4Y'],
  ['dx5jr', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopSSJmNf_F4zgSnZ-rqG4Y'],
  ['9dmm0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqU-T3lVf_F4zgSnZ-rqG4Y'],
  ['q9gjd', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat2TD5nHf_F4zgSnZ-rqG4Y'],
  ['nk9re', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatw9yJrnf_F4zgSnZ-rqG4Y'],
  ['lw2j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIpaZnvO5CB39rUUwyzOw'],
  ['e071e', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaolqCVLnf_F4zgSnZ-rqG4Y'],
  ['13ml', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap1lT9rvO5CB39rUUwyzOw'],
  ['m12d', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatFradnvO5CB39rUUwyzOw'],
  ['qd5kd', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat0-SDnHf_F4zgSnZ-rqG4Y'],
  ['dkgd8', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaoo9D9lOf_F4zgSnZ-rqG4Y'],
  ['keyz', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat8_0JPvO5CB39rUUwyzOw'],
  ['w0wm8', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauNqEXlOf_F4zgSnZ-rqG4Y'],
  ['9nwr', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqUgEZrvO5CB39rUUwyzOw'],
  ['lgkg7', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatI5lLk1f_F4zgSnZ-rqG4Y'],
  ['55kwg', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y'],
];

function fromB64url(s) {
  return [...Buffer.from(s.replace(/-/g,'+').replace(/_/g,'/'), 'base64')];
}

function buildFeatureBits(mangaId, charsToUse) {
  // Returns array of bits
  const bits = [];
  for (let i = 0; i < charsToUse; i++) {
	const ch = i < mangaId.length ? mangaId.charCodeAt(i) : 0;
	for (let bit = 0; bit < 8; bit++) {
	  bits.push((ch >> bit) & 1);
	}
  }
  return bits;
}

function popcount(bits) {
  return bits.reduce((s,b) => s+b, 0);
}

// GF(2) Gaussian elimination to solve A*x = b where A is matrix of feature bits
// Returns solution array or null if inconsistent
function solvGF2(A, b) {
  const m = A.length;    // equations
  const n = A[0].length; // unknowns

  // Augmented matrix
  const mat = A.map((row, i) => [...row, b[i]]);

  const pivotCol = [];
  let row = 0;
  for (let col = 0; col < n && row < m; col++) {
	// Find pivot
	let pivotRow = -1;
	for (let r = row; r < m; r++) {
	  if (mat[r][col] === 1) { pivotRow = r; break; }
	}
	if (pivotRow < 0) continue;

	// Swap
	[mat[row], mat[pivotRow]] = [mat[pivotRow], mat[row]];
	pivotCol.push({ row, col });

	// Eliminate
	for (let r = 0; r < m; r++) {
	  if (r !== row && mat[r][col] === 1) {
		for (let c = col; c <= n; c++) {
		  mat[r][c] ^= mat[row][c];
		}
	  }
	}
	row++;
  }

  // Check consistency
  for (let r = row; r < m; r++) {
	if (mat[r][n] === 1) return null; // inconsistent
  }

  // Extract solution (free variables = 0)
  const x = new Array(n).fill(0);
  for (const { row: r, col: c } of pivotCol) {
	x[c] = mat[r][n];
  }
  return x;
}

function maskBitsToNum(bits) {
  let n = 0n;
  for (let i = 0; i < bits.length; i++) {
	if (bits[i]) n |= 1n << BigInt(i);
  }
  return n;
}

// Separate long and short IDs
const longOracles = oracles.filter(([id]) => id.length > 4);
const shortOracles = oracles.filter(([id]) => id.length <= 4);

console.log(`Long oracles: ${longOracles.length}, Short oracles: ${shortOracles.length}`);

// Solve for long masks (40 output bits, 40 input bits)
const LONG_BITS = 40;
const A_long = longOracles.map(([id]) => buildFeatureBits(id, 5));
const tokens_long = longOracles.map(([, tok]) => fromB64url(tok));

// For each output bit, extract the observed value from each oracle
const newMasks_long = [];
for (let outputBit = 0; outputBit < LONG_BITS; outputBit++) {
  const byteIdx = 49 + (outputBit >> 3);
  const bitIdx = outputBit & 7;
  const b_col = tokens_long.map(bytes => (bytes[byteIdx] >> bitIdx) & 1);

  const solution = solvGF2(A_long, b_col);
  if (!solution) {
	console.log(`WARNING: inconsistent system for output bit ${outputBit}`);
	newMasks_long.push(0n);
  } else {
	newMasks_long.push(maskBitsToNum(solution));
  }
}

console.log('\nLong masks (40-bit):');
for (let i = 0; i < LONG_BITS; i++) {
  console.log(`  [${i}]: ${newMasks_long[i]}`);
}

// Solve for short masks (32 output bits, 32 input bits)
const SHORT_BITS = 32;
const A_short = shortOracles.map(([id]) => buildFeatureBits(id, 4));
const tokens_short = shortOracles.map(([, tok]) => fromB64url(tok));

const newMasks_short = [];
for (let outputBit = 0; outputBit < SHORT_BITS; outputBit++) {
  const byteIdx = 49 + (outputBit >> 3);
  const bitIdx = outputBit & 7;
  const b_col = tokens_short.map(bytes => (bytes[byteIdx] >> bitIdx) & 1);

  const solution = solvGF2(A_short, b_col);
  if (!solution) {
	console.log(`WARNING: inconsistent system for short output bit ${outputBit}`);
	newMasks_short.push(0n);
  } else {
	newMasks_short.push(maskBitsToNum(solution));
  }
}

console.log('\nShort masks (32-bit):');
for (let i = 0; i < SHORT_BITS; i++) {
  console.log(`  [${i}]: ${newMasks_short[i]}`);
}

// Verify against all oracles
function computeSignWithMasks(mangaId, masks_long, masks_short) {
  const isShort = mangaId.length <= 4;
  const charsToUse = isShort ? 4 : 5;
  const masks = isShort ? masks_short : masks_long;
  const numBits = isShort ? SHORT_BITS : LONG_BITS;

  const prefixBytes = [
	97,200,64,144,162,7,176,70,112,166,46,172,221,0,253,31,196,10,25,32,
	99,136,29,229,210,13,150,51,132,252,213,72,16,222,85,16,49,197,175,230,
	100,6,120,233,32,249,167,132,106,
  ];

  const suffixBytes_long = [127,241,120,206,4,167,103,234,234,27,134];
  const suffixBytes_short = [239,59,144,129,223,218,212,83,12,179,59];

  const suffix = isShort ? suffixBytes_short : suffixBytes_long;
  const varByteCount = isShort ? 4 : 5;
  const totalLen = 49 + varByteCount + suffix.length;
  const bytes = new Uint8Array(totalLen);

  for (let i = 0; i < prefixBytes.length; i++) bytes[i] = prefixBytes[i];

  const featureBits = buildFeatureBits(mangaId, charsToUse);

  for (let outputBit = 0; outputBit < numBits; outputBit++) {
	if (outputBit >= masks.length) break;
	const mask = masks[outputBit];
	// Compute parity of featureBits & mask
	let p = 0;
	for (let b = 0; b < featureBits.length; b++) {
	  if (featureBits[b] && ((mask >> BigInt(b)) & 1n)) p ^= 1;
	}
	if (p) {
	  const byteIdx = 49 + (outputBit >> 3);
	  const bitIdx = outputBit & 7;
	  bytes[byteIdx] |= (1 << bitIdx);
	}
  }

  for (let i = 0; i < suffix.length; i++) bytes[49 + varByteCount + i] = suffix[i];

  return Buffer.from(bytes).toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}

console.log('\nVerification:');
let pass = 0, fail = 0;
for (const [id, expected] of oracles) {
  const got = computeSignWithMasks(id, newMasks_long, newMasks_short);
  if (got === expected) {
	pass++;
  } else {
	fail++;
	console.log(`  FAIL ${id}`);
	console.log(`    exp: ${expected}`);
	console.log(`    got: ${got}`);
  }
}
console.log(`\n${pass}/${oracles.length} pass, ${fail} fail`);

// Output C# arrays
console.log('\n--- C# LiveVariableBitMasks (long, 40 elements) ---');
const csLong = newMasks_long.map(n => n.toString()).join(',\n\t\t');
console.log(`\t\t${csLong},`);

console.log('\n--- C# LiveVariableBitMasks4 (short, 32 elements) ---');
const csShort = newMasks_short.map(n => n.toString()).join(',\n\t\t');
console.log(`\t\t${csShort},`);
