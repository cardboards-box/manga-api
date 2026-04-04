// Detailed analysis of the signing algorithm
const fs = require('fs');

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

const m = runModule(9165);
const apiClient = m.apiClient;

async function testUrl(path, query) {
    lastSignedOptions = null;
    try {
        await apiClient.get(path, { query });
    } catch(e) {}
    if (lastSignedOptions) {
        const url = lastSignedOptions.url;
        const qmark = url.indexOf('?');
        if (qmark >= 0) {
            const params = new URLSearchParams(url.slice(qmark + 1));
            return { time: params.get('time'), _: params.get('_') };
        }
    }
    return null;
}

async function main() {
    // Test 1: Original known URL (should produce known signature)
    let r = await testUrl('/manga/60jxz/chapters', { limit: 20, page: 1, 'order[number]': 'desc' });
    console.log('\nTest 1 - /manga/60jxz/chapters (known):');
    console.log('time:', r?.time);
    console.log('_:', r?._);
    console.log('CORRECT:', r?._ === 'xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLrCwdJr4xWS0NeDGz_rNrbqBjLLP1H9qi');

    // Test 2: Different manga ID
    r = await testUrl('/manga/abc123/chapters', { limit: 20, page: 1 });
    console.log('\nTest 2 - /manga/abc123/chapters:');
    console.log('time:', r?.time);
    console.log('_:', r?._);

    // Test 3: Full URL (with https://)
    r = await testUrl('https://comix.to/api/v2/manga/60jxz/chapters', { limit: 20, page: 1 });
    console.log('\nTest 3 - full https URL:');
    console.log('time:', r?.time);
    console.log('_:', r?._);

    // Test 4: Different page
    r = await testUrl('/manga/60jxz/chapters', { limit: 20, page: 2 });
    console.log('\nTest 4 - /manga/60jxz/chapters page 2:');
    console.log('time:', r?.time);
    console.log('_:', r?._);

    // Test 5: Check what the signing message string looks like
    // We know: /manga/60jxz/chapters + sep + 0 + sep + 1 → known _
    // Try to find the separator by checking different possible messages
    // known _ = xQm9tJfLwGhz_0Eq8S_YAHYkwp-q1PLfm50W5QJnyd1NnNYpAjXjyCoAzoOLrCwdJr4xWS0NeDGz_rNrbqBjLLP1H9qi
    
    // Let's also capture signed request headers
    const savedFetch = global.fetch;
    global.fetch = async (url, opts) => {
        lastSignedOptions = { url, opts };
        return { ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) };
    };
    
    console.log('\nFull test complete.');
}

main().catch(console.error);
