// Get FULL byte arrays at each step 
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
const apiClient = m.apiClient;
global.fetch = async (url) => ({ ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) });

async function main() {
    trackCalls = true;
    allCalls = [];
    await apiClient.get('/manga/60jxz/chapters', { query: {} }).catch(() => {});
    trackCalls = false;
    
    console.log('const oracleStates = [');
    for (let i = 0; i < allCalls.length; i++) {
        const b = allCalls[i];
        console.log(`    // [${i}] len=${b.length}`);
        console.log(`    [${b.join(',')}],`);
    }
    console.log('];');
}

main().catch(console.error);
