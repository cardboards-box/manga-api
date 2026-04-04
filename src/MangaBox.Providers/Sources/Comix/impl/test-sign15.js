// Capture oracle states for multiple URLs, then analyze all transform tables
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

let allCalls = [];
let trackCalls = false;
String.fromCharCode.apply = function(thisArg, bytes) {
    if (trackCalls && Array.isArray(bytes) && bytes.length >= 5 && bytes.length <= 80) {
        allCalls.push([...bytes]);
    }
    return String.fromCharCode(...bytes);
};

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
const m = runModule(9165);

// Gather oracle states for multiple manga IDs
const mangaIds = ['60jxz', 'aaaaa', 'bbbbb', '12345', 'xxxxx', 'ABCDE', '99999', 'zzzzz'];
const oraclesByUrl = [];

async function getOracleStates(mangaId) {
    allCalls = [];
    trackCalls = true;
    global.fetch = async (url) => ({ ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) });
    await m.apiClient.get(`/manga/${mangaId}/chapters`, { query: {} }).catch(() => {});
    trackCalls = false;
    return allCalls.filter(c => c.length >= 5 && c.length <= 80).map(c => [...c]);
}

// Analysis setup - same as test-sign14.js
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
function findFns(input, expected) {
    return Object.entries(fns).filter(([n, fn]) => fn(input) === expected).map(([n]) => n);
}

// Accumulated candidates: for each (round, pos%10), track intersection
// Initialize with all functions as candidates, then intersect
const candidateSets = Array.from({length: 5}, () => 
    Array.from({length: 10}, () => new Set(Object.keys(fns)))
);

async function main() {
    for (const mangaId of mangaIds) {
        const states = await getOracleStates(mangaId);
        if (states.length < 6) continue;
        
        for (let round = 0; round < 5; round++) {
            const input = states[round];
            const output = states[round + 1];
            const pc = prependCounts[round];
            const rc4Out = rc4(rc4Keys[round], input);
            
            for (let i = 0; i < input.length; i++) {
                const xored = rc4Out[i] ^ xorKeys[round][i % 32];
                const pos = i % 10;
                let tByte = (i < pc) ? output[2 * i + 1] : output[pc + i];
                
                const matches = new Set(findFns(xored, tByte));
                // Intersect with existing candidates
                const existing = candidateSets[round][pos];
                for (const fn of [...existing]) {
                    if (!matches.has(fn)) existing.delete(fn);
                }
            }
        }
    }
    
    console.log('=== FINAL TRANSFORM TABLES ===');
    for (let round = 0; round < 5; round++) {
        console.log(`\nRound ${round + 1}:`);
        for (let pos = 0; pos < 10; pos++) {
            const candidates = [...candidateSets[round][pos]];
            console.log(`  pos%10=${pos}: ${candidates.join('/')}`);
        }
    }
}

main().catch(console.error);
