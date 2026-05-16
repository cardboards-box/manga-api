// Reverse-engineer the correct bitmasks from live oracle tokens
// We have oracle tokens for 40 different manga IDs
// The structure is: bytes[0..48] = fixed prefix, bytes[49..53] = variable, bytes[54..64] = fixed suffix
// Each variable bit is a linear function of the input character bits

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
  // known 55kwg from earlier
  ['55kwg', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y'],
];

function fromB64url(s) {
  return [...Buffer.from(s.replace(/-/g,'+').replace(/_/g,'/'), 'base64')];
}

// First, verify prefix and suffix are consistent
const prefixBytes = fromB64url(oracles[0][1]).slice(0, 49);
const suffixBytes_long = fromB64url(oracles[0][1]).slice(54);
console.log('Token length:', fromB64url(oracles[0][1]).length);
console.log('Prefix[0..48]:', prefixBytes.map(b=>b.toString(16).padStart(2,'0')).join(','));
console.log('Suffix[54..]:', suffixBytes_long.map(b=>b.toString(16).padStart(2,'0')).join(','));

// Check all long tokens (65 bytes) use 5 variable bytes, short (61 bytes) use 4
const longTokens = oracles.filter(([id,tok]) => fromB64url(tok).length === 65);
const shortTokens = oracles.filter(([id,tok]) => fromB64url(tok).length === 61);
console.log('\nLong tokens (5 var bytes):', longTokens.map(x=>x[0]).join(','));
console.log('Short tokens (4 var bytes):', shortTokens.map(x=>x[0]).join(','));

// Verify prefixes match
let prefixOk = true;
for (const [id, tok] of oracles) {
  const b = fromB64url(tok);
  for (let i = 0; i < 49; i++) {
	if (b[i] !== prefixBytes[i]) {
	  console.log(`PREFIX MISMATCH at ${id} byte ${i}: got ${b[i]} expected ${prefixBytes[i]}`);
	  prefixOk = false;
	}
  }
}
console.log('Prefix consistent:', prefixOk);

// Now extract the variable region bytes from each oracle
// For long IDs (5 chars, 5 var bytes): bytes 49-53
// For short IDs (≤4 chars, 4 var bytes): bytes 49-52
console.log('\nVariable region for each oracle:');
for (const [id, tok] of oracles) {
  const b = fromB64url(tok);
  const isShort = id.length <= 4;
  const varEnd = 49 + (isShort ? 4 : 5);
  const varBytes = b.slice(49, varEnd);
  const varBits = [];
  for (const byte of varBytes) {
	for (let bit = 0; bit < 8; bit++) {
	  varBits.push((byte >> bit) & 1);
	}
  }
  console.log(`  ${id} (${isShort?'short':'long'}): ${varBytes.map(b=>b.toString(16).padStart(2,'0')).join(' ')} bits: ${varBits.join('')}`);
}
