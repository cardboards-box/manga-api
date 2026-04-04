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

// Intercept fetch to capture signed request params
let lastSignedOptions = null;
global.fetch = async (url, opts) => {
    lastSignedOptions = { url, opts };
    return {
        ok: true,
        headers: { get: () => null, includes: () => false },
        json: async () => ({ status: 200, result: [] })
    };
};

const code = fs.readFileSync('3e8d29e151083399.js', 'utf-8');

// Build minimal TURBOPACK module system
const moduleRegistry = {};
const mods = [];

global.TURBOPACK = { push: (arr) => mods.push(arr) };

try {
    eval(code);
} catch(e) {
    console.log('Error running code:', e.message);
}

// Find module 9165 factory (at index 4 in the array)
const bundle = mods[0];
let module9165Factory = null;
for (let i = 1; i < bundle.length; i += 2) {
    if (bundle[i] === 9165) {
        module9165Factory = bundle[i + 1];
        console.log('Found module 9165 at index', i);
        break;
    }
}

if (!module9165Factory) {
    console.log('Module 9165 not found');
    process.exit(1);
}

// We need to create a minimal module runner that builds a dependency graph
// Module 9165 likely depends on other modules
// Let's create a simple require/exports system

const moduleCache = {};

function buildModuleMap() {
    const map = {};
    for (let i = 1; i < bundle.length; i += 2) {
        map[bundle[i]] = bundle[i + 1];
    }
    return map;
}
const moduleMap = buildModuleMap();
console.log('Available modules:', Object.keys(moduleMap));

function runModule(id) {
    if (moduleCache[id]) return moduleCache[id].exports;
    const factory = moduleMap[id];
    if (!factory) {
        console.log('Module not found:', id);
        return {};
    }
    const mod = { exports: {} };
    moduleCache[id] = mod;
    try {
        factory(mod, mod.exports, runModule);
    } catch(e) {
        console.log(`Error running module ${id}:`, e.message);
    }
    return mod.exports;
}

// TURBOPACK module API:
// e.i(id) = import module by id
// e.s(exports) = define exports
// e.n(id) = ??? 
// Let's build a proper TURBOPACK runtime

function createModuleContext(id) {
    const ctx = {
        i: (depId) => {
            return runModule(depId);
        },
        s: (exportDefs, chunkId) => {
            // Export definitions: array of [name, getter] pairs
            if (Array.isArray(exportDefs)) {
                for (let i = 0; i < exportDefs.length; i += 2) {
                    const name = exportDefs[i];
                    const getter = exportDefs[i + 1];
                    if (typeof getter === 'function' && moduleCache[id]) {
                        try {
                            moduleCache[id].exports[name] = getter();
                        } catch(e) {
                            // lazy getter
                            Object.defineProperty(moduleCache[id].exports, name, { get: getter, enumerable: true });
                        }
                    }
                }
            }
        },
        n: (depId) => runModule(depId)
    };
    return ctx;
}

function runModule(id) {
    if (moduleCache[id]) return moduleCache[id].exports;
    const factory = moduleMap[id];
    if (!factory) {
        //console.log('Module not found:', id);
        return {};
    }
    const mod = { exports: {} };
    moduleCache[id] = mod;
    const ctx = createModuleContext(id);
    try {
        factory(ctx);
    } catch(e) {
        console.log(`Error running module ${id}:`, e.message.substring(0, 100));
    }
    return mod.exports;
}

// Run module 9165
console.log('\nRunning module 9165...');
const result = runModule(9165);
console.log('Module 9165 exports:', typeof result, Object.keys(result));

// Explore all modules to find any interceptor/signing related exports
for (const id of Object.keys(moduleMap)) {
    if (isNaN(parseInt(id))) continue; // skip non-numeric module ids
    try {
        const m = runModule(parseInt(id));
        const keys = Object.keys(m);
        if (keys.some(k => k.toLowerCase().includes('sign') || k.toLowerCase().includes('chapter') || k.toLowerCase().includes('intercept') || k.toLowerCase().includes('request'))) {
            console.log(`\nModule ${id} has interesting exports:`, keys);
        }
    } catch(e) {}
}
