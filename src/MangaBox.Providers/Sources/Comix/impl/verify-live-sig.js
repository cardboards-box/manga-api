// Reproduce ComputeLiveSignature from ComixToSigner.cs in JS
const LiveTokenPrefix = Buffer.from([
  97,200,64,144,162,7,176,70,112,166,46,172,221,0,253,31,196,10,25,32,
  99,136,29,229,210,13,150,51,132,252,213,72,16,222,85,16,49,197,175,230,
  100,6,120,233,32,249,167,132,106
]);

const LiveVariableBitMasks = [
  105991738n,51780415n,106648650n,101453670n,102369579n,102369547n,1443341n,32n,
  105317703n,122032177n,4933456n,5785966n,38870390n,71703670n,105729616n,119299905n,
  4212491n,17781050n,122031462n,123668029n,100866919n,105336076n,102702646n,18498907n,
  118693414n,73028896n,34692474n,50988358n,35277078n,55513408n,102254145n,5330176n,
  122228004n,329020n,84033793n,34622328n,0n,21825298n,101321551n,123162950n,
];

const LiveVariableBitMasks4 = [
  105991738n,51780415n,106648650n,101453670n,102369579n,102369547n,1443341n,32n,
  105317703n,122032177n,4933456n,5785966n,38870390n,71703670n,105729616n,119299905n,
  4212491n,17781050n,122031462n,123668029n,100866919n,105336076n,102702646n,18498907n,
  118693414n,73028896n,34692474n,50988358n,35277078n,55513408n,102254145n,5330176n,
];

const LiveTokenSuffix = Buffer.from([127,241,120,206,4,167,103,234,234,27,134]);
const LiveTokenSuffix4 = Buffer.from([239,59,144,129,223,218,212,83,12,179,59]);

function popcount(n) {
  let count = 0n;
  while (n > 0n) { count += n & 1n; n >>= 1n; }
  return count;
}

function parity(n) {
  return Number(popcount(n) & 1n);
}

function buildFeatureBits(mangaId, charsToUse) {
  let bits = 0n;
  let bitIndex = 0n;
  for (let i = 0; i < charsToUse; i++) {
	const ch = i < mangaId.length ? mangaId.charCodeAt(i) : 0;
	for (let bit = 0; bit < 8; bit++) {
	  if ((ch >> bit) & 1) bits |= 1n << bitIndex;
	  bitIndex++;
	}
  }
  return bits;
}

function computeLiveSignature(mangaId) {
  const isShortId = mangaId.length <= 4;
  const variableMasks = isShortId ? LiveVariableBitMasks4 : LiveVariableBitMasks;
  const variableByteStart = 49;
  const variableByteCount = isShortId ? 4 : 5;
  const suffix = isShortId ? LiveTokenSuffix4 : LiveTokenSuffix;
  const suffixStart = variableByteStart + variableByteCount;

  const bytes = Buffer.alloc(suffixStart + suffix.length);
  LiveTokenPrefix.copy(bytes, 0);

  const featureBits = buildFeatureBits(mangaId, isShortId ? 4 : 5);
  for (let outputBit = 0; outputBit < variableMasks.length; outputBit++) {
	if (parity(featureBits & variableMasks[outputBit]) !== 0) {
	  const byteIndex = variableByteStart + (outputBit >> 3);
	  const bitIndex = outputBit & 7;
	  bytes[byteIndex] |= (1 << bitIndex);
	}
  }

  suffix.copy(bytes, suffixStart);

  return bytes.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}

const ids = ['55kwg', '60jxz', '8w6dm', 'aaaaa', '8w6d'];
for (const id of ids) {
  console.log(`${id}: ${computeLiveSignature(id)}`);
}

console.log('\nExpected from browser for 55kwg:');
console.log('  YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y');
console.log('Match:', computeLiveSignature('55kwg') === 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y');
