const fs = require('fs');

const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
const probe = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/q5-inexpr-probe.json', 'utf8'));

const encrypted = decodeBase64Url(oracle.e);
const keyString = probe.q5Key?.key;

if (!keyString) {
  console.log('missing keyString');
  process.exit(0);
}

const keyBytes = [...Buffer.from(keyString, 'latin1')];

const results = [];
results.push(['xor-key', xorWithKey(encrypted, keyBytes)]);
results.push(['rc4-key', rc4(keyBytes, encrypted)]);
results.push(['xor-then-rc4', rc4(keyBytes, xorWithKey(encrypted, keyBytes))]);
results.push(['rc4-then-xor', xorWithKey(rc4(keyBytes, encrypted), keyBytes)]);

for (const [name, bytes] of results) {
  const latin = Buffer.from(bytes).toString('latin1');
  const utf8 = Buffer.from(bytes).toString('utf8');
  console.log(name, {
	len: bytes.length,
	latinHead: JSON.stringify(latin.slice(0, 80)),
	utf8Head: JSON.stringify(utf8.slice(0, 80)),
	hasJsonLatin: latin.includes('{"status"') || latin.startsWith('{') || latin.startsWith('%7B'),
	hasJsonUtf8: utf8.includes('{"status"') || utf8.startsWith('{') || utf8.startsWith('%7B'),
  });
}

function decodeBase64Url(value) {
  let padded = value.replace(/-/g, '+').replace(/_/g, '/');
  while (padded.length % 4 !== 0) padded += '=';
  return [...Buffer.from(padded, 'base64')];
}

function xorWithKey(data, key) {
  const out = new Array(data.length);
  for (let i = 0; i < data.length; i++) {
	out[i] = data[i] ^ key[i % key.length];
  }
  return out;
}

function rc4(key, data) {
  const s = Array.from({ length: 256 }, (_, i) => i);
  let j = 0;
  for (let i = 0; i < 256; i++) {
	j = (j + s[i] + key[i % key.length]) & 255;
	[s[i], s[j]] = [s[j], s[i]];
  }

  const out = new Array(data.length);
  let x = 0;
  let y = 0;
  for (let i = 0; i < data.length; i++) {
	x = (x + 1) & 255;
	y = (y + s[x]) & 255;
	[s[x], s[y]] = [s[y], s[x]];
	out[i] = data[i] ^ s[(s[x] + s[y]) & 255];
  }

  return out;
}
