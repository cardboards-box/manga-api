// Use actual oracle to capture step-by-step byte arrays
// Intercept String.fromCharCode.apply to see final bytes before btoa
const fs = require('fs');

global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;

let btoaInputBytes = null;
global.btoa = (s) => {
    btoaInputBytes = Array.from({length: s.length}, (_, i) => s.charCodeAt(i));
    return Buffer.from(s, 'binary').toString('base64');
};
global.atob = (s) => Buffer.from(s, 'base64').toString('binary');
global.performance = { now: () => Date.now() };
global.location = { href: 'https://comix.to/', host: 'comix.to' };
global.fetch = async (url) => ({ ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) });

// Intercept String.fromCharCode.apply to capture intermediate byte arrays
// The p() function: String.fromCharCode.apply(null, bytes)
const origFromCharCode = String.fromCharCode.bind(String);
let fromCharCodeApplyCalls = [];
let trackFromCharCode = false;

const origStringApply = Function.prototype.apply;
String.fromCharCode.apply = function(thisArg, bytes) {
    if (trackFromCharCode && Array.isArray(bytes) && bytes.length >= 5 && bytes.length <= 80) {
        fromCharCodeApplyCalls.push([...bytes]);
    }
    return origFromCharCode.apply(origFromCharCode, bytes);
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
    trackFromCharCode = true;
    fromCharCodeApplyCalls = [];
    btoaInputBytes = null;
    
    await apiClient.get('/manga/60jxz/chapters', { query: {} }).catch(() => {});
    
    trackFromCharCode = false;
    
    console.log('\nAll String.fromCharCode.apply calls (len 5-80):');
    for (let i = 0; i < fromCharCodeApplyCalls.length; i++) {
        const b = fromCharCodeApplyCalls[i];
        console.log(`  [${i}] len=${b.length}: ${b.slice(0,8).join(',')}`);
    }
    
    console.log('\nFinal btoa input (', btoaInputBytes ? btoaInputBytes.length : 0, 'bytes):');
    if (btoaInputBytes) {
        console.log('  First 16:', btoaInputBytes.slice(0, 16).join(','));
        console.log('  All:', btoaInputBytes.join(','));
    }
    
    const p = new URLSearchParams(signedUrl.slice(signedUrl.indexOf('?')+1));
    console.log('\nExpected _:', p.get('_'));
}

main().catch(console.error);
