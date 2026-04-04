// Determine all 5 transform tables by comparing RC4 output to oracle states
const oracleStates = [
    // [0] len=35 (input message)
    [37,50,70,109,97,110,103,97,37,50,70,54,48,106,120,122,37,50,70,99,104,97,112,116,101,114,115,37,51,65,48,37,51,65,49],
    // [1] len=42 (after round 1)
    [202,43,179,246,254,138,17,210,80,27,53,8,15,32,162,187,229,118,25,103,153,60,17,236,237,34,167,13,69,196,51,114,72,47,166,21,236,101,39,107,242,247],
    // [2] len=48 (after round 2)
    [88,158,156,168,32,218,168,228,41,210,159,77,42,215,29,243,134,123,187,166,188,98,226,207,180,61,184,145,233,217,138,54,185,60,24,141,27,238,238,115,43,193,33,79,162,229,252,42],
    // [3] len=55 (after round 3)
    [213,184,37,68,17,162,121,157,137,55,66,226,68,172,33,89,140,27,155,60,125,49,210,121,120,149,24,216,76,255,31,189,81,116,98,72,134,6,62,29,41,214,250,202,112,145,1,167,50,176,204,36,219,21,107],
    // [4] len=63 (after round 4)
    [231,61,104,76,131,154,170,124,60,136,229,42,169,126,239,37,33,89,145,139,99,214,53,38,111,98,20,69,151,17,124,148,210,95,231,5,167,86,223,175,154,200,92,53,88,171,38,116,26,102,122,238,227,76,159,201,143,236,211,59,52,185,80],
    // [5] len=69 (final)
    [197,9,189,180,151,203,192,104,115,255,65,42,241,47,216,0,118,36,194,159,170,212,242,223,155,157,22,229,2,103,201,221,77,156,214,41,2,53,227,200,42,0,206,131,139,172,44,29,38,190,49,89,45,13,120,49,179,254,179,107,110,160,99,44,179,245,31,218,162],
];

const prependCounts = [7, 6, 7, 8, 6];

// All keys base64-encoded
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
    'yrP+EVA1Dw==',
    'WJwgqCmf',
    '1SUReYlCRA==',
    '52iDqjzlqe8=',
    'xb2XwHNB',
].map(k => [...Buffer.from(k, 'base64')]);

// RC4
function rc4(key, data) {
    const S = Array.from({length: 256}, (_, i) => i);
    let j = 0;
    for (let i = 0; i < 256; i++) {
        j = (j + S[i] + key[i % key.length]) % 256;
        [S[i], S[j]] = [S[j], S[i]];
    }
    let x = 0, y = 0;
    return data.map(b => {
        x = (x + 1) % 256;
        y = (y + S[x]) % 256;
        [S[x], S[y]] = [S[y], S[x]];
        return b ^ S[(S[x] + S[y]) % 256];
    });
}

// Transform functions
const fns = {
    'c':  b => (b + 115) % 256,
    'b':  b => (b - 12 + 256) % 256,
    's':  b => (b + 143) % 256,
    'h':  b => (b - 42 + 256) % 256,
    'k':  b => (b + 15) % 256,
    '_':  b => (b - 20 + 256) % 256,
    'f':  b => (b - 188 + 256) % 256,
    'm':  b => b ^ 177,
    'y':  b => ((b >>> 1) | (b << 7)) & 255,
    'g':  b => ((b << 2) | (b >>> 6)) & 255,
    '$':  b => ((b << 4) | (b >>> 4)) & 255,
};

function findFn(input, expected) {
    const matches = [];
    for (const [name, fn] of Object.entries(fns)) {
        if (fn(input) === expected) matches.push(name);
    }
    return matches;
}

// For each round, figure out the transform table
for (let round = 0; round < 5; round++) {
    const input = oracleStates[round];
    const expectedOutput = oracleStates[round + 1];
    const pc = prependCounts[round];
    
    const rc4Out = rc4(rc4Keys[round], input);
    
    // Build transform result array: 
    // For i < pc: output[2*i] = prependKey[i], output[2*i+1] = t[i]
    // For i >= pc: output[pc + i] = t[i]
    
    // Verify prepend bytes:
    let prependOk = true;
    for (let i = 0; i < pc; i++) {
        if (expectedOutput[2 * i] !== prependKeys[round][i]) {
            console.log(`Round ${round+1}: prepend[${i}] mismatch! expected ${prependKeys[round][i]}, got ${expectedOutput[2*i]}`);
            prependOk = false;
        }
    }
    if (prependOk) console.log(`Round ${round+1}: prepend bytes OK`);
    
    // Gather transform function for each i
    const tableByPos = {};
    let allUnique = true;
    
    for (let i = 0; i < input.length; i++) {
        const xored = rc4Out[i] ^ xorKeys[round][i % 32];
        const pos = i % 10;
        
        // Find expected transformed byte in output
        let tByte;
        if (i < pc) {
            tByte = expectedOutput[2 * i + 1];
        } else {
            tByte = expectedOutput[pc + i];
        }
        
        const matches = findFn(xored, tByte);
        
        if (matches.length === 0) {
            console.log(`Round ${round+1}, i=${i}, pos%10=${pos}: xored=${xored}, expected=${tByte} → NO MATCH!`);
        } else if (matches.length > 1) {
            if (!tableByPos[pos]) tableByPos[pos] = new Set();
            matches.forEach(m => tableByPos[pos].add(m));
            // ambiguous, try to narrow down with other instances
        } else {
            if (!tableByPos[pos]) tableByPos[pos] = new Set([matches[0]]);
            else {
                // intersect: we need the function to be consistent
                const existing = tableByPos[pos];
                const next = new Set([matches[0]]);
                if (!existing.has(matches[0])) {
                    console.log(`Round ${round+1}, pos%10=${pos}: CONFLICT! was ${[...existing].join(',')}, now ${matches[0]} (i=${i}, xored=${xored}, expected=${tByte})`);
                    allUnique = false;
                }
                tableByPos[pos] = new Set([matches[0]]);
            }
        }
    }
    
    console.log(`Round ${round+1} transform table:`);
    for (let pos = 0; pos <= 9; pos++) {
        const fset = tableByPos[pos];
        const display = fset ? [...fset].join('/') : '???';
        console.log(`  pos%10=${pos}: ${display}`);
    }
    console.log('');
}
