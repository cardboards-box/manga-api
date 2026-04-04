// Deep analysis of the signing algorithm
// Intercept internal functions to understand what they do
const fs = require('fs');

global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;

// Intercept btoa and atob to trace calls
let btoaCallCount = 0, atobCallCount = 0, atobCalls = [], btoaCalls = [];
global.btoa = (s) => {
    btoaCallCount++;
    const result = Buffer.from(s, 'binary').toString('base64');
    btoaCalls.push({input: s, inputLen: s.length, output: result});
    return result;
};
global.atob = (s) => {
    atobCallCount++;
    const result = Buffer.from(s, 'base64').toString('binary');
    atobCalls.push({input: s, inputLen: s.length, outputLen: result.length});
    return result;
};
global.performance = { now: () => Date.now() };
global.location = { href: 'https://comix.to/', host: 'comix.to' };
global.fetch = async (url, opts) => {
    return { ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) };
};

// Intercept encodeURIComponent
const origEncode = global.encodeURIComponent;
let encodeCalls = [];
// We can't easily override encodeURIComponent... but we can log from the code

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
            const mod = moduleCache[id];
            if (!mod) return;
            let i = 0;
            while (i < defs.length) {
                const name = defs[i];
                if (typeof name !== 'string') { i++; continue; }
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
    const factory = moduleMap[id];
    if (!factory) return {};
    const mod = { exports: {} };
    moduleCache[id] = mod;
    try { factory(createCtx(id)); } catch(e) {}
    return mod.exports;
}

// Reset tracking
btoaCallCount = 0; atobCallCount = 0; btoaCalls = []; atobCalls = [];

const m = runModule(9165);
const apiClient = m.apiClient;

console.log('After module init:');
console.log('btoa called:', btoaCallCount, 'times');
console.log('atob called:', atobCallCount, 'times');

// Reset for the actual request
btoaCallCount = 0; atobCallCount = 0; btoaCalls = []; atobCalls = [];

let reqUrl = null;
global.fetch = async (url, opts) => {
    reqUrl = url;
    return { ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) };
};

async function main() {
    await apiClient.get('/manga/60jxz/chapters', { query: { limit: 20, page: 1 } }).catch(() => {});
    
    console.log('\nDuring signing:');
    console.log('btoa called:', btoaCallCount, 'times');
    console.log('atob called:', atobCallCount, 'times');
    
    console.log('\nAll atob (decode) calls:');
    for (let i = 0; i < atobCalls.length; i++) {
        const c = atobCalls[i];
        console.log(`  [${i}] input: "${c.input.substring(0, 40)}" → outputLen: ${c.outputLen}`);
        // Show first 8 bytes as hex
        const bytes = Buffer.from(global.atob(c.input), 'binary');
        console.log(`       first 8 bytes: ${Array.from(bytes.slice(0,8)).map(b=>b.toString(16).padStart(2,'0')).join(' ')}`);
    }
    
    console.log('\nAll btoa (encode) calls:');
    for (let i = 0; i < btoaCalls.length; i++) {
        const c = btoaCalls[i];
        console.log(`  [${i}] inputLen: ${c.inputLen} → "${c.output.substring(0, 40)}..."`);
    }
    
    console.log('\nFinal URL:', reqUrl);
}

main().catch(console.error);
