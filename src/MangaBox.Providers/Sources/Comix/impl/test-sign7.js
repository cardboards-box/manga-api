// Extract the signing function and expose it directly
// Then generate test vectors
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

const m = runModule(9165);
const apiClient = m.apiClient;

async function sign(url, time, bodyLen) {
    let signedUrl = null;
    const origFetch = global.fetch;
    global.fetch = async (u) => { signedUrl = u; return { ok: true, headers: { get: () => null }, json: async () => ({ status: 200, result: [] }) }; };
    
    // We need to force a specific time value
    // The signing uses: time = (url.startsWith(prefix)) ? 1 : timestamp
    // Since relative paths give time=1, we can only test time=1 for now
    await apiClient.get(url, { query: {} }).catch(() => {});
    global.fetch = origFetch;
    
    if (!signedUrl) return null;
    const p = new URLSearchParams(signedUrl.slice(signedUrl.indexOf('?') + 1));
    return { time: p.get('time'), _: p.get('_') };
}

async function main() {
    // Test various URL patterns to understand what's in the message
    const tests = [
        '/manga/60jxz/chapters',    // known value
        '/manga/a/chapters',         // shortest possible
        '/manga/aa/chapters',
        '/manga/aaa/chapters',
        '/manga/aaaa/chapters',
        '/manga/aaaaa/chapters',
        '/manga/aaaaaa/chapters',
        '/manga/ab/chapters',
    ];
    
    console.log('=== Signing oracle test vectors ===');
    for (const url of tests) {
        const result = await sign(url, 1, 0);
        if (result) {
            console.log(`url="${url}" (len=${url.length}): time=${result.time}, _len=${result._.length}, _=${result._}`);
        }
    }
    
    // Also compute encodeURIComponent of various message strings to find the 69-char one
    console.log('\n=== Separator investigation ===');
    const separators = ['|', '?', '/', ':', ',', ';', '@', '#', '!', ' ', '\t', '\n', '.', '-', '_'];
    for (const sep of separators) {
        const msg = `/manga/60jxz/chapters${sep}0${sep}1`;
        const encoded = encodeURIComponent(msg);
        console.log(`sep="${sep}" (${sep.charCodeAt(0)}): encoded_len=${encoded.length}, encoded="${encoded.substring(0, 50)}"`);
    }
}

main().catch(console.error);
