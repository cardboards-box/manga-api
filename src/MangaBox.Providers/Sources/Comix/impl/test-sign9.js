// Extract the exact RC4 keys by intercepting atob (t0vtS) during signing
// The signing function calls t0vtS() (which is atob) to decode keys
// We need to capture the SPECIFIC atob calls that happen during signing

const fs = require('fs');

global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;

// We'll track atob calls by count
let atobCallCount = 0;
let atobLog = [];
let trackAtob = false;

global.btoa = (s) => Buffer.from(s, 'binary').toString('base64');
global.atob = (s) => {
    const r = Buffer.from(s, 'base64').toString('binary');
    if (trackAtob) {
        atobLog.push({ idx: atobCallCount, input: s, outputLen: r.length, outputHex: Buffer.from(r, 'binary').toString('hex').substring(0,32) });
    }
    atobCallCount++;
    return r;
};

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

const m = runModule(9165);
const apiClient = m.apiClient;

let signedUrl = null;
global.fetch = async (url) => {
    signedUrl = url;
    return { ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) };
};

async function main() {
    // Do a warmup call to prime any caches
    trackAtob = false;
    atobCallCount = 0;
    atobLog = [];
    
    // Now track atob ONLY during the actual signing
    trackAtob = true;
    atobLog = [];
    const beforeCount = atobCallCount;
    
    await apiClient.get('/manga/60jxz/chapters', { query: {} }).catch(() => {});
    
    trackAtob = false;
    
    console.log(`Total atob calls during signing: ${atobLog.length}`);
    console.log('\nAll atob calls during signing:');
    for (let i = 0; i < atobLog.length; i++) {
        const c = atobLog[i];
        console.log(`  [${i}] "${c.input}" → len=${c.outputLen} hex: ${c.outputHex}`);
    }
    
    // Also check: let's run a second signing to compare
    signedUrl = null;
    trackAtob = true;
    atobLog = [];
    
    await apiClient.get('/manga/abc123/chapters', { query: {} }).catch(() => {});
    
    trackAtob = false;
    console.log('\n\nFor /manga/abc123/chapters:');
    for (let i = 0; i < atobLog.length; i++) {
        const c = atobLog[i];
        console.log(`  [${i}] "${c.input}" → len=${c.outputLen} hex: ${c.outputHex}`);
    }
    
    console.log('\nSigned URL:', signedUrl);
    const p = new URLSearchParams(signedUrl.slice(signedUrl.indexOf('?') + 1));
    console.log('_:', p.get('_'));
}

main().catch(console.error);
