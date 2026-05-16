// Compare computed vs browser token byte by byte
const browser = 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y';
const computed = 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaptflM3kf_F4zgSnZ-rqG4Y';

function fromB64url(s) {
  return Buffer.from(s.replace(/-/g,'+').replace(/_/g,'/'), 'base64');
}

const bBuf = fromB64url(browser);
const cBuf = fromB64url(computed);

console.log('Browser length:', bBuf.length);
console.log('Computed length:', cBuf.length);

console.log('\nByte-by-byte comparison:');
const maxLen = Math.max(bBuf.length, cBuf.length);
for (let i = 0; i < maxLen; i++) {
  const b = i < bBuf.length ? bBuf[i] : -1;
  const c = i < cBuf.length ? cBuf[i] : -1;
  if (b !== c) {
	console.log(`  byte[${i}]: browser=${b.toString(16).padStart(2,'0')} (${b}) vs computed=${c.toString(16).padStart(2,'0')} (${c}) -- DIFF`);
  }
}

console.log('\nBits that differ in variable region (bytes 49-53):');
for (let i = 49; i < 54; i++) {
  if (i >= bBuf.length || i >= cBuf.length) break;
  const b = bBuf[i];
  const c = cBuf[i];
  if (b !== c) {
	const xor = b ^ c;
	console.log(`  byte[${i}]: browser=${b.toString(2).padStart(8,'0')} computed=${c.toString(2).padStart(8,'0')} diff_bits=${xor.toString(2).padStart(8,'0')}`);
  }
}

// Show all bits of variable region
console.log('\nAll variable region bits (bytes 49-53):');
for (let i = 49; i < 54; i++) {
  if (i >= bBuf.length) break;
  const b = bBuf[i];
  const c = cBuf[i];
  console.log(`  byte[${i}]: browser=${b.toString(2).padStart(8,'0')} (0x${b.toString(16).padStart(2,'0')}) computed=${c.toString(2).padStart(8,'0')} (0x${c.toString(16).padStart(2,'0')}) ${b===c?'MATCH':'DIFF'}`);
}
