// Targeted extraction: get the message string and observe signing
const fs = require('fs');

global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;

// Track encodeURIComponent and btoa calls ONLY during signing
let trackingEnabled = false;
let trackedEncode = [], trackedBtoa = [], trackedAtob = [];

const origEncode = global.encodeURIComponent;
global.encodeURIComponent = function(s) {
    const r = origEncode(s);
    if (trackingEnabled) {
        trackedEncode.push({ input: s, output: r });
    }
    return r;
};

global.btoa = (s) => {
    const r = Buffer.from(s, 'binary').toString('base64');
    if (trackingEnabled && trackedBtoa.length < 5) {
        trackedBtoa.push({ inputLen: s.length, outputLen: r.length, output: r });
    }
    return r;
};

global.atob = (s) => {
    const r = Buffer.from(s, 'base64').toString('binary');
    if (trackingEnabled && trackedAtob.length < 20) {
        trackedAtob.push({ input: s, inputLen: s.length, outputLen: r.length });
    }
    return r;
};

global.performance = { now: () => Date.now() };
global.location = { href: 'https://comix.to/', host: 'comix.to' };
global.fetch = async (url) => {
    return { ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) };
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
const apiClient = m.apiClient;

let signedUrl = null;
global.fetch = async (url) => {
    signedUrl = url;
    return { ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) };
};

async function main() {
    // Enable tracking only during signing
    trackingEnabled = true;
    trackedEncode = []; trackedBtoa = []; trackedAtob = [];
    
    await apiClient.get('/manga/60jxz/chapters', { query: { limit: 20, page: 1 } }).catch(() => {});
    
    trackingEnabled = false;
    
    console.log('=== encodeURIComponent calls during signing ===');
    for (let i = 0; i < Math.min(trackedEncode.length, 10); i++) {
        const c = trackedEncode[i];
        console.log(`  [${i}]: input="${c.input.substring(0, 80)}" → output="${c.output.substring(0, 80)}"`);
    }
    
    console.log('\n=== atob (decode) calls during signing (first 20) ===');
    for (let i = 0; i < trackedAtob.length; i++) {
        const c = trackedAtob[i];
        console.log(`  [${i}]: "${c.input}" → ${c.outputLen} bytes`);
    }
    
    console.log('\n=== btoa (encode) calls during signing (first 5) ===');
    for (let i = 0; i < trackedBtoa.length; i++) {
        const c = trackedBtoa[i];
        console.log(`  [${i}]: ${c.inputLen} bytes → "${c.output.substring(0, 60)}"`);
    }
    
    console.log('\nSigned URL:', signedUrl);
    const qmark = signedUrl.indexOf('?');
    if (qmark >= 0) {
        const p = new URLSearchParams(signedUrl.slice(qmark + 1));
        console.log('time:', p.get('time'));
        console.log('_:', p.get('_'));
    }
}

main().catch(console.error);
