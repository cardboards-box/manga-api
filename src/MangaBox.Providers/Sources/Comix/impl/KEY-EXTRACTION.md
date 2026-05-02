# Comix.to Signing Keys — How to Re-Extract When They Change

The signing keys are hardcoded in `ComixToSigner.cs`. If the Comix.to site deploys a new
JS bundle, the keys will almost certainly change. This document explains exactly how to find
and re-extract them.

---

## Step 0 — Get the New JS Bundle

The signing code lives in a TURBOPACK chunk served under `/_next/static/chunks/`.
Open the Comix.to title page (e.g. `https://comix.to/title/<slug>`) in your browser,
open DevTools → Network, filter by JS, and look for the chunk that contains the string
`withSignature` or `apiClient`. Download it and replace `impl/3e8d29e151083399.js`.

The filename hash will differ; just rename accordingly and update the `readFileSync` path
in all the helper scripts in this folder.

---

## Step 1 — Find the Signing Module ID

The file uses the TURBOPACK bundle format. Each module is registered as:

```
(globalThis.TURBOPACK||[]).push([scriptEl, moduleId, factory, ...])
```

The signing module is the one that exports both `apiClient` and `withCache`.
Search the bundle file for the string `withCache` — the module ID is the number
on the line that closes/opens the surrounding factory function.

In the current bundle, that module is **9165**. Confirm with:

```bash
node -e "
const fs = require('fs');
const code = fs.readFileSync('3e8d29e151083399.js', 'utf-8');
const idx = code.indexOf('9165');
console.log(code.slice(idx - 20, idx + 200));
"
```

Look for the pattern `withCache` near a number — that number is the new module ID.
Update all scripts that reference `runModule(9165)`.

---

## Step 2 — Extract the 15 Keys via atob Interception

The signing module calls `atob()` (aliased as `t0vtS` in the obfuscated code) to decode every
key at runtime. Intercepting `atob` during a single signing call captures all 15 keys in
call-order: 5 RC4 keys, then 5 XOR keys, then 5 prepend keys (each group in round order).

Run the following (also saved as **`test-sign9.js`**):

```bash
node test-sign9.js
```

The output lists every `atob` call with the base64 input and the resulting hex bytes.
Filter for calls with output length of **32 bytes** (RC4 and XOR keys) and **6–8 bytes**
(prepend keys). The 15 keys appear in this exactly order across the two signing calls:

```
Calls 0-4  : RC4 keys (32 bytes each)
Calls 5-9  : XOR keys (32 bytes each)
Calls 10-14: Prepend keys (6–8 bytes each)
```

Update `ComixToSigner.cs` with the new base64 strings.

### Manual search alternative

If the atob interception doesn't isolate the keys cleanly, search the bundle directly.
The keys are present as base64 literals (length 44 for 32-byte keys, 8–12 for prepend keys)
near the `switch(...%10)` pattern:

```bash
node -e "
const fs = require('fs');
const code = fs.readFileSync('3e8d29e151083399.js', 'utf-8');
// Find base64 strings of length 44 (32-byte keys)
const b64 = [...code.matchAll(/\"([A-Za-z0-9+\/]{43}=)\"/g)].map(m => m[1]);
console.log('32-byte keys:', b64);
"
```

---

## Step 3 — Verify / Re-derive the Prepend Counts

The prepend counts `[7, 6, 7, 8, 6]` come from the prepend key lengths. Each prepend key's
byte length equals the prepend count for that round. If the keys change, the counts simply
follow from `Buffer.from(base64Key, 'base64').length`.

---

## Step 4 — Re-derive the Transform Tables

The transform tables are determined by five `switch(i % 10)` statements inside the signing
loop. If the bundle changes, run the automated analysis pipeline:

### 4a — Capture oracle byte-arrays at each round boundary

Run **`test-sign13.js`** to get the full arrays (needs no changes other than the bundle filename):

```bash
node test-sign13.js
```

It prints `const oracleStates = [...]` — 6 arrays for the 5 rounds (input + output of each).

### 4b — Determine transform functions by intersection

Run **`test-sign15.js`** with the new keys substituted in:

```bash
node test-sign15.js
```

This runs 8 different manga IDs through the oracle, intersects the candidate function set for
each `(round, pos % 10)` pair down to a single function, and prints the final tables.

The 11 possible transform functions are:

| Name | Operation       | C# equivalent |
|------|-----------------|---------------|
| `c`  | `+115 mod 256`  | `(byte)((v + 115) & 0xFF)` |
| `b`  | `-12 mod 256`   | `(byte)((v - 12 + 256) & 0xFF)` |
| `s`  | `+143 mod 256`  | `(byte)((v + 143) & 0xFF)` |
| `h`  | `-42 mod 256`   | `(byte)((v - 42 + 256) & 0xFF)` |
| `k`  | `+15 mod 256`   | `(byte)((v + 15) & 0xFF)` |
| `_`  | `-20 mod 256`   | `(byte)((v - 20 + 256) & 0xFF)` |
| `f`  | `-188 mod 256`  | `(byte)((v - 188 + 256) & 0xFF)` |
| `m`  | `XOR 177`       | `(byte)(v ^ 177)` |
| `y`  | `ROR 1`         | `(byte)(((v >> 1) \| (v << 7)) & 0xFF)` |
| `g`  | `ROL 2`         | `(byte)(((v << 2) \| (v >> 6)) & 0xFF)` |
| `$`  | `ROL 4 (nibble swap)` | `(byte)(((v << 4) \| (v >> 4)) & 0xFF)` |

If any ambiguity remains after 8 URLs, read the raw switch statements from the bundle:

```bash
node -e "
const fs = require('fs');
const code = fs.readFileSync('3e8d29e151083399.js', 'utf-8');
const idx = code.indexOf('9165');
const region = code.slice(idx, idx + 80000);
const switches = [...region.matchAll(/switch\s*\([^)]{1,30}%\s*10\s*\)\s*\{[^}]{1,2000}}/gs)];
switches.forEach((m, i) => { console.log('--- round', i, '---'); console.log(m[0].substring(0, 800)); });
"
```

---

## Step 5 — Verify the Full Implementation

Run **`test-sign16.js`** after updating the keys and transform tables.
It tests 5 known oracle outputs and must print `5/5 tests passed`:

```bash
node test-sign16.js
```

Then update the `const` arrays in `ComixToSigner.cs` and rebuild:

```bash
cd ../../.. && dotnet build MangaBox.Providers\MangaBox.Providers.csproj --no-restore
```

---

## Quick-Reference: Module Loading Boilerplate

All test scripts share this TURBOPACK bootstrap — only the bundle filename needs updating:

```javascript
const fs = require('fs');
global.document = { currentScript: null };
global.window = global;
global.navigator = { userAgent: 'Node.js', clipboard: null };
global.TextEncoder = require('util').TextEncoder;
global.btoa = (s) => Buffer.from(s, 'binary').toString('base64');
global.atob = (s) => Buffer.from(s, 'base64').toString('binary');
global.performance = { now: () => Date.now() };
global.location = { href: 'https://comix.to/', host: 'comix.to' };
global.fetch = async () => ({ ok: true, headers: { get: () => null }, json: async () => ({}) });

const mods = [];
global.TURBOPACK = { push: (arr) => mods.push(arr) };
eval(fs.readFileSync('3e8d29e151083399.js', 'utf-8'));  // ← update filename here

const moduleMap = {};
const bundle = mods[0];
for (let i = 1; i < bundle.length; i += 2)
    if (typeof bundle[i] === 'number') moduleMap[bundle[i]] = bundle[i + 1];

// Mock the Next.js env module (required dependency of module 9165)
const moduleCache = {};
moduleCache[85696] = { exports: { default: { env: { DEBUG_REQUEST: 'false' } } } };

function runModule(id) {
    if (moduleCache[id]) return moduleCache[id].exports;
    const factory = moduleMap[id];
    if (!factory) return {};
    const mod = { exports: {} };
    moduleCache[id] = mod;
    factory({
        i: (depId) => runModule(depId),
        s: (defs) => { /* export helper — copy from any test script */ }
    });
    return mod.exports;
}

const { apiClient } = runModule(9165);  // ← signing module
```
