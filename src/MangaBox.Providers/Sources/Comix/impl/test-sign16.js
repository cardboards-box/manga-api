// Verify complete algorithm implementation
const rc4Keys = [
    '13YDu67uDgFczo3DnuTIURqas4lfMEPADY6Jaeqky+w=',
    'vZ23RT7pbSlxwiygkHd1dhToIku8SNHPC6V36L4cnwM=',
    'BkWI8feqSlDZKMq6awfzWlUypl88nz65KVRmpH0RWIc=',
    'RougjiFHkSKs20DZ6BWXiWwQUGZXtseZIyQWKz5eG34=',
    'U9LRYFL2zXU4TtALIYDj+lCATRk/EJtH7/y7qYYNlh8=',
].map(k => [...Buffer.from(k, 'base64')]);
const xorKeys = [
    'yEy7wBfBc+gsYPiQL/4Dfd0pIBZFzMwrtlRQGwMXy3Q=',
    'QX0sLahOByWLcWGnv6l98vQudWqdRI3DOXBdit9bxCE=',
    'v7EIpiQQjd2BGuJzMbBA0qPWDSS+wTJRQ7uGzZ6rJKs=',
    'LL97cwoDoG5cw8QmhI+KSWzfW+8VehIh+inTxnVJ2ps=',
    'e/GtffFDTvnw7LBRixAD+iGixjqTq9kIZ1m0Hj+s6fY=',
].map(k => [...Buffer.from(k, 'base64')]);
const prependKeys = [
    'yrP+EVA1Dw==', 'WJwgqCmf', '1SUReYlCRA==', '52iDqjzlqe8=', 'xb2XwHNB',
].map(k => [...Buffer.from(k, 'base64')]);
const prependCounts = [7, 6, 7, 8, 6];

// Transform tables (round → pos%10 → function)
const transforms = [
    // Round 1
    [b => (b+115)%256, b => (b-12+256)%256, b => ((b>>>1)|(b<<7))&255, b => ((b<<4)|(b>>>4))&255, b => (b-42+256)%256, b => (b+143)%256, b => (b-42+256)%256, b => (b+15)%256, b => ((b>>>1)|(b<<7))&255, b => (b+115)%256],
    // Round 2
    [b => (b+115)%256, b => (b-12+256)%256, b => ((b<<4)|(b>>>4))&255, b => (b-42+256)%256, b => (b+143)%256, b => (b+15)%256, b => ((b<<4)|(b>>>4))&255, b => (b-20+256)%256, b => (b+115)%256, b => (b+143)%256],
    // Round 3
    [b => (b+115)%256, b => (b-188+256)%256, b => (b+143)%256, b => ((b<<2)|(b>>>6))&255, b => ((b>>>1)|(b<<7))&255, b => b^177, b => ((b<<4)|(b>>>4))&255, b => (b+15)%256, b => (b+143)%256, b => (b-12+256)%256],
    // Round 4
    [b => (b-12+256)%256, b => b^177, b => ((b>>>1)|(b<<7))&255, b => (b+143)%256, b => (b-20+256)%256, b => (b+143)%256, b => (b-20+256)%256, b => ((b>>>1)|(b<<7))&255, b => ((b>>>1)|(b<<7))&255, b => b^177],
    // Round 5
    [b => (b-20+256)%256, b => (b+143)%256, b => (b+115)%256, b => b^177, b => (b-12+256)%256, b => b^177, b => (b-188+256)%256, b => (b+143)%256, b => ((b<<4)|(b>>>4))&255, b => ((b<<2)|(b>>>6))&255],
];

function rc4(key, data) {
    const S = Array.from({length: 256}, (_, i) => i);
    let j = 0;
    for (let i = 0; i < 256; i++) {
        j = (j + S[i] + key[i % key.length]) % 256;
        [S[i], S[j]] = [S[j], S[i]];
    }
    let x = 0, y = 0;
    return data.map(b => {
        x = (x + 1) % 256; y = (y + S[x]) % 256;
        [S[x], S[y]] = [S[y], S[x]]; return b ^ S[(S[x] + S[y]) % 256];
    });
}

function sign(url) {
    const bodyLen = 0, time = 1;
    const message = encodeURIComponent(`${url}:${bodyLen}:${time}`);
    let bytes = [...message].map(c => c.charCodeAt(0));
    
    for (let round = 0; round < 5; round++) {
        const rc4Out = rc4(rc4Keys[round], bytes);
        const pc = prependCounts[round];
        const output = [];
        for (let i = 0; i < bytes.length; i++) {
            if (i < pc) output.push(prependKeys[round][i]);
            const xored = rc4Out[i] ^ xorKeys[round][i % 32];
            output.push(transforms[round][i % 10](xored));
        }
        bytes = output;
    }
    
    const binary = String.fromCharCode(...bytes);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// Tests
const tests = [
    ['/manga/60jxz/chapters', 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLrCwdJr4xWS0NeDGz_rNrbqBjLLP1H9qi'],
    ['/manga/aaaaa/chapters', 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLpgAUa20xWS0NeDGz_rNrbqBjLLP1H9qi'],
    ['/manga/bbbbb/chapters', 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLpMEVcD0xWS0NeDGz_rNrbqBjLLP1H9qi'],
    ['/manga/12345/chapters', 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLRq3iAioxWS0NeDGz_rNrbqBjLLP1H9qi'],
    ['/manga/xxxxx/chapters', 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLMT6nJhMxWS0NeDGz_rNrbqBjLLP1H9qi'],
];

let pass = 0;
for (const [url, expected] of tests) {
    const result = sign(url);
    const ok = result === expected;
    if (ok) pass++;
    console.log(`${ok ? 'PASS' : 'FAIL'}: ${url}`);
    if (!ok) {
        console.log(`  expected: ${expected}`);
        console.log(`  got:      ${result}`);
    }
}
console.log(`\n${pass}/${tests.length} tests passed`);
