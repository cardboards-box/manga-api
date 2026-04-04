// Test script to extract the signing algorithm from 3e8d29e151083399.js
const fs = require('fs');

// Mock browser globals
global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;
global.btoa = (s) => Buffer.from(s, 'binary').toString('base64');
global.atob = (s) => Buffer.from(s, 'base64').toString('binary');
global.performance = { now: () => Date.now() };
global.location = { href: 'https://comix.to/', host: 'comix.to' };

let lastSignedOptions = null;
global.fetch = async (url, opts) => {
    lastSignedOptions = { url, opts };
    return {
        ok: true,
        headers: { get: () => null },
        json: async () => ({ status: 200, result: [] })
    };
};

const code = fs.readFileSync('3e8d29e151083399.js', 'utf-8');
const mods = [];
global.TURBOPACK = { push: (arr) => mods.push(arr) };

try { eval(code); } catch(e) { console.log('Error loading:', e.message.substring(0, 100)); }

const bundle = mods[0];
const moduleMap = {};
for (let i = 1; i < bundle.length; i += 2) {
    if (typeof bundle[i] === 'number') moduleMap[bundle[i]] = bundle[i + 1];
}

const moduleCache = {};

// Pre-register mocked dependencies
moduleCache[85696] = { exports: { default: { env: { DEBUG_REQUEST: 'false' } } } };

function createCtx(id) {
    return {
        i: (depId) => runModule(depId),
        s: (defs, chunkId) => {
            if (!Array.isArray(defs)) return;
            const mod = moduleCache[id];
            if (!mod) return;
            let i = 0;
            while (i < defs.length) {
                const name = defs[i];
                if (typeof name !== 'string') { i++; continue; }
                const next = defs[i + 1];
                if (next === 0 || next === null) {
                    mod.exports[name] = defs[i + 2];
                    i += 3;
                } else if (typeof next === 'function') {
                    try {
                        Object.defineProperty(mod.exports, name, { get: next, enumerable: true, configurable: true });
                    } catch { mod.exports[name] = next(); }
                    i += 2;
                } else {
                    mod.exports[name] = next;
                    i += 2;
                }
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
    try { factory(createCtx(id)); } catch(e) { /* ignore */ }
    return mod.exports;
}

const m = runModule(9165);
console.log('Module 9165 exports:', Object.keys(m));

const apiClient = m.apiClient;
if (!apiClient) { console.log('apiClient not found'); process.exit(1); }

async function test() {
    try {
        await apiClient.get('/manga/60jxz/chapters', {
            query: { limit: 20, page: 1, 'order[number]': 'desc' }
        });
    } catch(e) {
        console.log('Request error:', e.message);
    }
    
    if (lastSignedOptions) {
        const url = lastSignedOptions.url;
        console.log('\nSigned URL:', url);
        const qmark = url.indexOf('?');
        if (qmark >= 0) {
            const params = new URLSearchParams(url.slice(qmark + 1));
            console.log('time:', params.get('time'));
            console.log('_:', params.get('_'));
        }
    } else {
        console.log('No request captured');
    }
    
    // Now try with fixed time=1 to match the known example
    // Reset capture
    lastSignedOptions = null;
    
    // Override Math.random (or whatever timestamp function) to return value that gives time=1
    // From the code: time = url.startsWith(prefix) ? 1 : y4aRXr[N4(36)]()
    // Means time=1 when URL starts with a specific prefix, which suggest a special debug/test URL
    console.log('\nDone.');
}

test().catch(e => console.log('Error:', e.message));
