// Determine exact switch cases for each transform round
// by working backwards from the oracle byte arrays
const fs = require('fs');

const atob = (s) => Buffer.from(s, 'base64').toString('binary');
const getBytes = (b64) => {
    const str = atob(b64);
    return Array.from({length: str.length}, (_, i) => str.charCodeAt(i));
};

// RC4 function
function rc4(key, data) {
    const S = new Array(256);
    for (let i = 0; i < 256; i++) S[i] = i;
    let j = 0;
    for (let i = 0; i < 256; i++) {
        j = (j + S[i] + key[i % key.length]) % 256;
        [S[i], S[j]] = [S[j], S[i]];
    }
    const result = [];
    let x = 0, y = 0;
    for (let i = 0; i < data.length; i++) {
        x = (x + 1) % 256;
        y = (y + S[x]) % 256;
        [S[x], S[y]] = [S[y], S[x]];
        result.push(data[i] ^ S[(S[x] + S[y]) % 256]);
    }
    return result;
}

const keys = {
    rc4: [
        '13YDu67uDgFczo3DnuTIURqas4lfMEPADY6Jaeqky+w=',
        'vZ23RT7pbSlxwiygkHd1dhToIku8SNHPC6V36L4cnwM=',
        'BkWI8feqSlDZKMq6awfzWlUypl88nz65KVRmpH0RWIc=',
        'RougjiFHkSKs20DZ6BWXiWwQUGZXtseZIyQWKz5eG34=',
        'U9LRYFL2zXU4TtALIYDj+lCATRk/EJtH7/y7qYYNlh8='
    ],
    xor: [
        'yEy7wBfBc+gsYPiQL/4Dfd0pIBZFzMwrtlRQGwMXy3Q=',
        'QX0sLahOByWLcWGnv6l98vQudWqdRI3DOXBdit9bxCE=',
        'v7EIpiQQjd2BGuJzMbBA0qPWDSS+wTJRQ7uGzZ6rJKs=',
        'LL97cwoDoG5cw8QmhI+KSWzfW+8VehIh+inTxnVJ2ps=',
        'e/GtffFDTvnw7LBRixAD+iGixjqTq9kIZ1m0Hj+s6fY='
    ],
    prepend: [
        'yrP+EVA1Dw==',
        'WJwgqCmf',
        '1SUReYlCRA==',
        '52iDqjzlqe8=',
        'xb2XwHNB'
    ]
};

// Oracle data (captured from String.fromCharCode.apply calls)
const oracleStates = [
    // [0] Initial message bytes (35)
    [37,50,70,109,97,110,103,97,37,50,70,54,48,106,120,122,37,50,70,99,104,97,112,116,101,114,115,37,51,65,48,37,51,65,49],
    // [1] After transform round 1 (42) = RC4 round 2 input
    [202,43,179,246,254,138,17,210,179,246,254,138,17,210,179,246,254,138,17,210,202,43,179,246,254,138,17,210,202,43,179,246,254,138,17,210,202,43,179,246,254,138],
    // [2] After transform round 2 (48) = RC4 round 3 input  
    [88,158,156,168,32,218,168,228,156,168,32,218,168,228,156,168,32,218,168,228,88,158,156,168,32,218,168,228,88,158,156,168,32,218,168,228,88,158,156,168,32,218,168,228,88,158,156,168],
    // [3] After transform round 3 (55) = RC4 round 4 input  
    [213,184,37,68,17,162,121,157,37,68,17,162,121,157,37,68,17,162,121,157,37,68,17,162,121,157,213,184,37,68,17,162,121,157,213,184,37,68,17,162,121,157,213,184,37,68,17,162,121,157,213,184,37,68,17],
    // [4] After transform round 4 (63) = RC4 round 5 input
    [231,61,104,76,131,154,170,124,231,61,104,76,131,154,170,124,231,61,104,76,131,154,170,124,231,61,104,76,131,154,170,124,231,61,104,76,131,154,170,124,231,61,104,76,131,154,170,124,231,61,104,76,131,154,170,124,231,61,104,76,131,154,170],
    // [5] After transform round 5 (69) = btoa input
    [197,9,189,180,151,203,192,104,115,255,65,42,241,47,216,0,118,36,194,159,170,212,242,223,155,157,22,229,2,103,201,221,77,156,214,41,2,53,227,200,42,0,206,131,139,172,44,29,38,190,49,89,45,13,120,49,179,254,179,107,110,160,99,44,179,245,31,218,162]
];

// First: recompute the FULL oracle states from actual oracle run
// The oracle states above are partial. Load the ACTUAL full arrays from one of our test runs.

console.log('=== Verifying RC4 rounds ===');

// RC4 round 1: input = oracleStates[0] (35 bytes), key = rc4_key1
// Output should be what goes into transform 1
const rc4_1_input = oracleStates[0];  // 35 bytes
const rc4_1_key = getBytes(keys.rc4[0]);
const rc4_1_output = rc4(rc4_1_key, rc4_1_input);  // 35 bytes

console.log('\nRC4 round 1 output (35 bytes, first 8):', rc4_1_output.slice(0, 8).join(','));

// Now we need to get transform 1 input = rc4_1_output (35 bytes)
// Transform 1 produces 42 bytes (oracleStates[1]) from 35 bytes
// For EACH position i in 0..34:
//   if i < 7: prepend prependKey[i]
//   xored = rc4_1_output[i] ^ xorKey1[i%32]
//   transformed = some_fn(xored) based on i%10
//   output: [prependKey[i], transformed, ...]
const xorKey1 = getBytes(keys.xor[0]);
const prependKey1 = getBytes(keys.prepend[0]);
// oracleState[1] = 42 bytes

// Figure out transform function for each position (0-9) in round 1
console.log('\nRound 1 transform determination:');
let outputIdx = 0;
for (let i = 0; i < rc4_1_output.length; i++) {
    const prependCount = prependKey1.length;  // 7
    if (i < prependCount) {
        // prepend should match
        if (oracleStates[1][outputIdx] !== prependKey1[i]) {
            console.log(`  ERROR at pos ${i}: expected prepend ${prependKey1[i]} got ${oracleStates[1][outputIdx]}`);
        }
        outputIdx++;
    }
    
    const xored = rc4_1_output[i] ^ xorKey1[i % 32];
    const actual_transformed = oracleStates[1][outputIdx];
    console.log(`  pos ${i} (mod10=${i%10}): byte=${rc4_1_output[i]} xored=${xored} → output=${actual_transformed} (fn=${findFn(xored, actual_transformed)})`);
    outputIdx++;
    if (i >= 9) break;  // just check first 10 positions
}

// Helper to identify which transform function was used
function findFn(input, output) {
    const n = 256;
    if ((input + 115) % 256 === output) return 'c (+115)';
    if ((input - 12 + 256) % 256 === output) return 'b (-12)';
    if ((input + 143) % 256 === output) return 's (+143)';
    if ((input - 42 + 256) % 256 === output) return 'h (-42)';
    if ((input + 15) % 256 === output) return 'k (+15)';
    if ((input - 20 + 256) % 256 === output) return '_ (-20)';
    if ((input - 188 + 256) % 256 === output) return 'f (-188)';
    if ((input ^ 177) === output) return 'm (^177)';
    if (((input >>> 1) | (input << 7)) & 255 === output) return 'y/l (ror1)';
    if (((input << 2) | (input >>> 6)) & 255 === output) return 'g (rol2)';
    if (((input << 4) | (input >>> 4)) & 255 === output) return '$ (rol4)';
    if (input === output) return 'identity';
    return `UNKNOWN (${input} → ${output})`;
}

// Now do all 5 rounds
console.log('\n=== Switch cases for all rounds ===');
let prevOutput = oracleStates[0];  // 35 bytes initial

for (let round = 0; round < 5; round++) {
    const rc4Key = getBytes(keys.rc4[round]);
    const xorKey = getBytes(keys.xor[round]);
    const prependKey = getBytes(keys.prepend[round]);
    
    const rc4Output = rc4(rc4Key, prevOutput);
    const nextOutput = oracleStates[round + 1];
    
    console.log(`\nRound ${round + 1} transform (prepend ${prependKey.length} bytes, input ${rc4Output.length} bytes → output ${nextOutput.length} bytes):`);
    
    // For first 10 positions, identify the transform function:
    const maxPos = Math.min(rc4Output.length, 10);  // check first 10 positions
    let outIdx = 0;
    for (let i = 0; i < maxPos; i++) {
        if (i < prependKey.length) outIdx++;  // skip prepend
        const xored = rc4Output[i] ^ xorKey[i % 32];
        const actual = nextOutput[outIdx];
        const fn = findFn(xored, actual);
        console.log(`  pos ${i} (mod10=${i%10}): xored=${xored} → ${actual} = ${fn}`);
        outIdx++;
    }
    
    prevOutput = nextOutput;
}
