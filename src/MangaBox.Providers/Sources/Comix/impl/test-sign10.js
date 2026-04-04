// Full algorithm trace - step by step byte arrays
const fs = require('fs');

global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;
global.btoa = (s) => Buffer.from(s, 'binary').toString('base64');
global.atob = (s) => Buffer.from(s, 'base64').toString('binary');
global.performance = { now: () => Date.now() };
global.location = { href: 'https://comix.to/', host: 'comix.to' };
global.fetch = async (url) => ({ ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) });

const code = fs.readFileSync('3e8d29e151083399.js', 'utf-8');
const mods = [];
global.TURBOPACK = { push: (arr) => mods.push(arr) };
try { eval(code); } catch(e) {}

const bundle = mods[0];
const moduleMap = {};
for (let i = 1; i < bundle.length; i += 2) {
    if (typeof bundle[i] === 'number') moduleMap[bundle[i]] = bundle[i + 1];
}

const moduleCache = {};
moduleCache[85696] = { exports: { default: { env: { DEBUG_REQUEST: 'false' } } } };

function createCtx(id) {
    return {
        i: (depId) => runModule(depId),
        s: (defs) => {
            if (!Array.isArray(defs)) return;
            const mod = moduleCache[id]; if (!mod) return;
            let i = 0;
            while (i < defs.length) {
                const name = defs[i]; if (typeof name !== 'string') { i++; continue; }
                const next = defs[i + 1];
                if (next === 0 || next === null) { mod.exports[name] = defs[i + 2]; i += 3; }
                else if (typeof next === 'function') { try { Object.defineProperty(mod.exports, name, { get: next, enumerable: true, configurable: true }); } catch { mod.exports[name] = next(); } i += 2; }
                else { mod.exports[name] = next; i += 2; }
            }
        }
    };
}

function runModule(id) {
    if (moduleCache[id]) return moduleCache[id].exports;
    const factory = moduleMap[id]; if (!factory) return {};
    const mod = { exports: {} }; moduleCache[id] = mod;
    try { factory(createCtx(id)); } catch(e) {}
    return mod.exports;
}

// Extract the 5 RC4 keys and 5 XOR keys and 5 prepend keys from the atob calls
const knownKeys = {
    rc4: [
        '13YDu67uDgFczo3DnuTIURqas4lfMEPADY6Jaeqky+w=',   // round 1
        'vZ23RT7pbSlxwiygkHd1dhToIku8SNHPC6V36L4cnwM=',   // round 2
        'BkWI8feqSlDZKMq6awfzWlUypl88nz65KVRmpH0RWIc=',   // round 3
        'RougjiFHkSKs20DZ6BWXiWwQUGZXtseZIyQWKz5eG34=',   // round 4
        'U9LRYFL2zXU4TtALIYDj+lCATRk/EJtH7/y7qYYNlh8='    // round 5
    ],
    xor: [
        'yEy7wBfBc+gsYPiQL/4Dfd0pIBZFzMwrtlRQGwMXy3Q=',   // round 1
        'QX0sLahOByWLcWGnv6l98vQudWqdRI3DOXBdit9bxCE=',   // round 2
        'v7EIpiQQjd2BGuJzMbBA0qPWDSS+wTJRQ7uGzZ6rJKs=',   // round 3
        'LL97cwoDoG5cw8QmhI+KSWzfW+8VehIh+inTxnVJ2ps=',   // round 4
        'e/GtffFDTvnw7LBRixAD+iGixjqTq9kIZ1m0Hj+s6fY='    // round 5
    ],
    prepend: [
        'yrP+EVA1Dw==',    // round 1 - 7 bytes
        'WJwgqCmf',        // round 2 - 6 bytes
        '1SUReYlCRA==',    // round 3 - 7 bytes
        '52iDqjzlqe8=',    // round 4 - 8 bytes
        'xb2XwHNB'         // round 5 - 6 bytes
    ]
};

// Verify by decoding
for (let r = 0; r < 5; r++) {
    const rc4 = Buffer.from(global.atob(knownKeys.rc4[r]), 'binary');
    const xor = Buffer.from(global.atob(knownKeys.xor[r]), 'binary');
    const pre = Buffer.from(global.atob(knownKeys.prepend[r]), 'binary');
    console.log(`Round ${r+1}: rc4=${rc4.length}b xor=${xor.length}b prepend=${pre.length}b`);
    console.log(`  rc4 bytes: ${rc4.toString('hex')}`);
    console.log(`  xor bytes: ${xor.toString('hex')}`);
    console.log(`  prepend bytes: ${pre.toString('hex')}`);
}

// Now manually implement the algorithm step by step and trace each state
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

// Transform functions
function c(b) { return (b + 115) % 256; }
function bsub(b) { return (b - 12 + 256) % 256; }
function s(b) { return (b + 143) % 256; }
function h(b) { return (b - 42 + 256) % 256; }
function k(b) { return (b + 15) % 256; }  // (b-241+256)%256 = (b+15)%256
function y(b) { return ((b >>> 1) | (b << 7)) & 255; }  // rot right 1
function l(b) { return ((b >>> 1) | (b << 7)) & 255; }  // rot right 1 (same as y)
function g(b) { return ((b << 2) | (b >>> 6)) & 255; }  // rot left 2
function dollar(b) { return ((b << 4) | (b >>> 4)) & 255; }  // rot left 4 (nibble swap)
function m(b) { return b ^ 177; }
function f(b) { return (b - 188 + 256) % 256; }  // (b+68)%256
function underscore(b) { return (b - 20 + 256) % 256; }  // (b+236)%256

// Transform functions per round
const transforms = [
    // Round 1
    (b, pos) => {
        switch (pos % 10) {
            case 0: case 9: return c(b);
            case 1: return bsub(b);
            case 2: return y(b);
            case 3: return dollar(b);
            case 4: case 6: return h(b);
            case 5: return s(b);
            case 7: return k(b);
            case 8: return l(b);
        }
    },
    // Round 2
    (b, pos) => {
        switch (pos % 10) {
            case 0: case 8: return c(b);
            case 1: return bsub(b);
            case 2: return dollar(b);
            case 3: return h(b);
            case 4: case 9: return s(b);
            case 5: return k(b);
            case 6: return dollar(b);  // inline = same as $
            case 7: return underscore(b);
        }
    },
    // Round 3
    (b, pos) => {
        switch (pos % 10) {
            case 0: return c(b);
            case 1: return f(b);
            case 2: case 8: return s(b);
            case 3: return g(b);
            case 4: return y(b);
            case 5: return m(b);
            case 6: return dollar(b);
            case 7: return k(b);
            case 9: return bsub(b);
        }
    },
    // Round 4
    (b, pos) => {
        switch (pos % 10) {
            case 0: return underscore(b);
            case 1: return f(b);
            case 2: case 8: return s(b);
            case 3: return g(b);
            case 4: return y(b);
            case 5: return m(b);
            case 6: return dollar(b);
            case 7: return k(b);
            case 9: return bsub(b);
        }
    },
    // Round 5
    (b, pos) => {
        switch (pos % 10) {
            case 0: return underscore(b);
            case 1: case 9: return m(b);
            case 2: case 7: return l(b);
            case 3: case 5: return s(b);
            case 4: case 6: return underscore(b);
            case 8: return y(b);
        }
    }
];

const prependCounts = [7, 6, 7, 8, 6];

function applyTransform(bytes, roundIdx) {
    const xorKey = [...Buffer.from(global.atob(knownKeys.xor[roundIdx]), 'binary')].map(c => c.charCodeAt ? c.charCodeAt(0) : c);
    const prependKey = Buffer.from(global.atob(knownKeys.prepend[roundIdx]), 'binary');
    const prependCount = prependCounts[roundIdx];
    const tf = transforms[roundIdx];
    
    const result = [];
    for (let i = 0; i < bytes.length; i++) {
        if (i < prependCount) {
            result.push(prependKey[i]);  // Insert prepend byte BEFORE transformed byte
        }
        let b = bytes[i];
        b = b ^ xorKey[i % 32];  // XOR with key
        b = tf(b, i);             // Apply transform based on position
        result.push(b & 255);
    }
    return result;
}

// Actually the xorKey check above might be wrong - let me decode xorKey correctly
function getByteArray(b64) {
    const str = global.atob(b64);
    return Array.from({length: str.length}, (_, i) => str.charCodeAt(i));
}

function applyTransformFixed(bytes, roundIdx) {
    const xorKey = getByteArray(knownKeys.xor[roundIdx]);
    const prependKey = getByteArray(knownKeys.prepend[roundIdx]);
    const prependCount = prependCounts[roundIdx];
    const tf = transforms[roundIdx];
    
    const result = [];
    for (let i = 0; i < bytes.length; i++) {
        if (i < prependCount) {
            result.push(prependKey[i]);  // prepend byte before the transformed byte
        }
        let b = bytes[i];
        b = b ^ xorKey[i % 32];
        b = tf(b, i);
        result.push(b & 255);
    }
    return result;
}

// Full signing function
function sign(url, bodyLen, time) {
    const message = encodeURIComponent(url + ':' + bodyLen + ':' + time);
    console.log('\nMessage:', message);
    console.log('Message length:', message.length);
    
    let bytes = Array.from({length: message.length}, (_, i) => message.charCodeAt(i));
    console.log('Initial bytes (' + bytes.length + '):', bytes.slice(0,8).join(','), '...');
    
    for (let round = 0; round < 5; round++) {
        const rc4Key = getByteArray(knownKeys.rc4[round]);
        bytes = rc4(rc4Key, bytes);
        console.log(`After RC4 round ${round+1} (${bytes.length} bytes):`, bytes.slice(0,8).join(','), '...');
        
        bytes = applyTransformFixed(bytes, round);
        console.log(`After transform round ${round+1} (${bytes.length} bytes):`, bytes.slice(0,8).join(','), '...');
    }
    
    // Convert bytes to string and base64url encode
    const str = bytes.map(b => String.fromCharCode(b)).join('');
    const b64 = global.btoa(str);
    const result = b64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    return result;
}

console.log('\n=== Reconstructed algorithm ===');
const computed = sign('/manga/60jxz/chapters', 0, 1);
const expected = 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLrCwdJr4xWS0NeDGz_rNrbqBjLLP1H9qi';
console.log('\nComputed:', computed);
console.log('Expected:', expected);
console.log('MATCH:', computed === expected);
