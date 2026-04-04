// Intercept the x() function to see the raw message bytes
// x() converts a string to char codes: e.split("").map(c => c.charCodeAt(0))
// It's called immediately after U$bs8p(message) to get the byte array

const fs = require('fs');

global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;

let btoaOrig = (s) => Buffer.from(s, 'binary').toString('base64');
let atobOrig = (s) => Buffer.from(s, 'base64').toString('binary');
global.btoa = btoaOrig;
global.atob = atobOrig;

global.performance = { now: () => Date.now() };
global.location = { href: 'https://comix.to/', host: 'comix.to' };
global.fetch = async (url) => ({ ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) });

// Intercept Array.prototype.map to catch the x() function call
// x(s) = s.split("").map(c => c.charCodeAt(0))
// When charCodeAt is called with argument 0, we're in x()

let captureNextArrayMap = false;
let capturedMessage = null;

const origArrayMap = Array.prototype.map;
Array.prototype.map = function(fn, ...rest) {
    // Try to detect x() by checking if fn uses charCodeAt
    if (captureNextArrayMap && this.length > 5 && typeof fn === 'function') {
        const fnSrc = fn.toString();
        if (fnSrc.includes('charCodeAt') || fnSrc.includes('u](0)') || fnSrc.includes('[u](0)')) {
            capturedMessage = this.join('');
            captureNextArrayMap = false;
            console.log('!!! Captured x() input: "' + capturedMessage.substring(0, 100) + '"');
        }
    }
    return origArrayMap.call(this, fn, ...rest);
};

// Alternative: intercept String.prototype.split
const origStrSplit = String.prototype.split;
let capturedSplits = [];
let trackSplits = false;
String.prototype.split = function(sep, ...rest) {
    if (trackSplits && sep === '' && this.length > 5 && this.length < 200) {
        capturedSplits.push(this.toString());
    }
    return origStrSplit.call(this, sep, ...rest);
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
    // Track string.split("") with tracking enabled only during signing
    trackSplits = true;
    capturedSplits = [];
    
    await apiClient.get('/manga/60jxz/chapters', { query: {} }).catch(() => {});
    
    trackSplits = false;
    
    // The x() function split is called multiple times:
    // 1. First call: on the message string (after U$bs8p encoding)
    // 2. After each RC4 round: on the RC4 output bytes-as-string
    // The FIRST unique split result that contains the URL path (possibly encoded) is the message
    console.log('\nAll split("") calls during signing (with len 5-200):');
    const unique = [...new Set(capturedSplits.map(s => s.substring(0, 80)))];
    for (let i = 0; i < Math.min(unique.length, 30); i++) {
        const s = unique[i];
        console.log(`  [${i}] len=${capturedSplits.find(x => x.startsWith(s.substring(0, 5)))?.length}: "${s}"`);
    }
    
    console.log('\nSigned URL:', signedUrl);
    const p = new URLSearchParams(signedUrl.slice(signedUrl.indexOf('?') + 1));
    console.log('time:', p.get('time'));
    console.log('_:', p.get('_'));
}

main().catch(console.error);
