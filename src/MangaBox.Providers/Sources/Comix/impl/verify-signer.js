// Verify what the RC4-based signer produces for known IDs
const rc4Keys = ['13YDu67uDgFczo3DnuTIURqas4lfMEPADY6Jaeqky+w=','vZ23RT7pbSlxwiygkHd1dhToIku8SNHPC6V36L4cnwM=','BkWI8feqSlDZKMq6awfzWlUypl88nz65KVRmpH0RWIc=','RougjiFHkSKs20DZ6BWXiWwQUGZXtseZIyQWKz5eG34=','U9LRYFL2zXU4TtALIYDj+lCATRk/EJtH7/y7qYYNlh8='].map(k=>[...Buffer.from(k,'base64')]);
const xorKeys = ['yEy7wBfBc+gsYPiQL/4Dfd0pIBZFzMwrtlRQGwMXy3Q=','QX0sLahOByWLcWGnv6l98vQudWqdRI3DOXBdit9bxCE=','v7EIpiQQjd2BGuJzMbBA0qPWDSS+wTJRQ7uGzZ6rJKs=','LL97cwoDoG5cw8QmhI+KSWzfW+8VehIh+inTxnVJ2ps=','e/GtffFDTvnw7LBRixAD+iGixjqTq9kIZ1m0Hj+s6fY='].map(k=>[...Buffer.from(k,'base64')]);
const prependKeys = ['yrP+EVA1Dw==','WJwgqCmf','1SUReYlCRA==','52iDqjzlqe8=','xb2XwHNB'].map(k=>[...Buffer.from(k,'base64')]);
const prependCounts = [7,6,7,8,6];
const transforms = [
  [b=>(b+115)%256,b=>(b-12+256)%256,b=>((b>>>1)|(b<<7))&255,b=>((b<<4)|(b>>>4))&255,b=>(b-42+256)%256,b=>(b+143)%256,b=>(b-42+256)%256,b=>(b+15)%256,b=>((b>>>1)|(b<<7))&255,b=>(b+115)%256],
  [b=>(b+115)%256,b=>(b-12+256)%256,b=>((b<<4)|(b>>>4))&255,b=>(b-42+256)%256,b=>(b+143)%256,b=>(b+15)%256,b=>((b<<4)|(b>>>4))&255,b=>(b-20+256)%256,b=>(b+115)%256,b=>(b+143)%256],
  [b=>(b+115)%256,b=>(b-188+256)%256,b=>(b+143)%256,b=>((b<<2)|(b>>>6))&255,b=>((b>>>1)|(b<<7))&255,b=>b^177,b=>((b<<4)|(b>>>4))&255,b=>(b+15)%256,b=>(b+143)%256,b=>(b-12+256)%256],
  [b=>(b-12+256)%256,b=>b^177,b=>((b>>>1)|(b<<7))&255,b=>(b+143)%256,b=>(b-20+256)%256,b=>(b+143)%256,b=>(b-20+256)%256,b=>((b>>>1)|(b<<7))&255,b=>((b>>>1)|(b<<7))&255,b=>b^177],
  [b=>(b-20+256)%256,b=>(b+143)%256,b=>(b+115)%256,b=>b^177,b=>(b-12+256)%256,b=>b^177,b=>(b-188+256)%256,b=>(b+143)%256,b=>((b<<4)|(b>>>4))&255,b=>((b<<2)|(b>>>6))&255]
];

function rc4(key, data) {
  const S = Array.from({length:256}, (_,i)=>i);
  let j = 0;
  for (let i = 0; i < 256; i++) {
	j = (j + S[i] + key[i % key.length]) % 256;
	[S[i], S[j]] = [S[j], S[i]];
  }
  let x = 0, y = 0;
  return data.map(b => {
	x = (x + 1) % 256; y = (y + S[x]) % 256;
	[S[x], S[y]] = [S[y], S[x]];
	return b ^ S[(S[x] + S[y]) % 256];
  });
}

function sign(url) {
  const msg = encodeURIComponent(url + ':0:1');
  let bytes = [...msg].map(c => c.charCodeAt(0));
  for (let r = 0; r < 5; r++) {
	const out = rc4(rc4Keys[r], bytes);
	const pc = prependCounts[r];
	const res = [];
	for (let i = 0; i < bytes.length; i++) {
	  if (i < pc) res.push(prependKeys[r][i]);
	  const x = out[i] ^ xorKeys[r][i % 32];
	  res.push(transforms[r][i % 10](x));
	}
	bytes = res;
  }
  return Buffer.from(bytes).toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}

// Chapter list tests
console.log('Chapter list tokens:');
for (const id of ['55kwg', '60jxz', '8w6dm', 'aaaaa', '60jxz']) {
  console.log(`  /manga/${id}/chapters -> ${sign('/manga/' + id + '/chapters')}`);
}

// Known good from test-sign16.js
console.log('\nKnown vectors from test-sign16.js:');
const tests = [
  ['/manga/60jxz/chapters', 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLrCwdJr4xWS0NeDGz_rNrbqBjLLP1H9qi'],
  ['/manga/aaaaa/chapters', 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLpgAUa20xWS0NeDGz_rNrbqBjLLP1H9qi'],
];
for (const [url, expected] of tests) {
  const got = sign(url);
  console.log(`  ${got === expected ? 'PASS' : 'FAIL'}: ${url}`);
  if (got !== expected) {
	console.log(`    expected: ${expected}`);
	console.log(`    got:      ${got}`);
  }
}

// Chapter detail
console.log('\nChapter detail tokens:');
console.log('  /chapters/9343749 ->', sign('/chapters/9343749'));
console.log('  /chapters/55kwg   ->', sign('/chapters/55kwg'));

// Now check live token for 55kwg chapter list
console.log('\nLive browser token for 55kwg chapter list:');
console.log('  Expected: YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y');
console.log('  RC4 sign: ' + sign('/manga/55kwg/chapters'));
console.log('  Match?', sign('/manga/55kwg/chapters') === 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y');
