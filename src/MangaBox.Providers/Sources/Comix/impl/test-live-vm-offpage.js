const fs = require('fs');
const util = require('util');

globalThis.globalThis = globalThis;
globalThis.window = globalThis;
globalThis.self = globalThis;
globalThis.document = {
  currentScript: null,
  createElement: () => ({}),
  addEventListener: () => {},
  removeEventListener: () => {},
  dispatchEvent: () => true,
  querySelector: () => null,
  querySelectorAll: () => [],
};

try {
  Object.defineProperty(globalThis, 'navigator', {
	value: { appCodeName: 'Mozilla', userAgent: 'Mozilla/5.0', platform: 'Win32', language: 'en-US' },
	configurable: true,
	writable: true,
  });
} catch (e) {
  globalThis.navigator = globalThis.navigator || {};
  globalThis.navigator.appCodeName = globalThis.navigator.appCodeName || 'Mozilla';
  globalThis.navigator.userAgent = globalThis.navigator.userAgent || 'Mozilla/5.0';
  globalThis.navigator.platform = globalThis.navigator.platform || 'Win32';
  globalThis.navigator.language = globalThis.navigator.language || 'en-US';
}

globalThis.location = { href: 'https://comix.to/', host: 'comix.to', origin: 'https://comix.to' };
globalThis.performance = { now: () => Date.now() };
globalThis.history = { pushState: () => {}, replaceState: () => {}, state: null };
globalThis.screen = { width: 1920, height: 1080 };
globalThis.window.document = globalThis.document;
globalThis.window.addEventListener = globalThis.window.addEventListener || (() => {});
globalThis.window.removeEventListener = globalThis.window.removeEventListener || (() => {});
globalThis.window.dispatchEvent = globalThis.window.dispatchEvent || (() => true);
globalThis.queueMicrotask = globalThis.queueMicrotask || ((cb) => cb && cb());
globalThis.requestAnimationFrame = globalThis.requestAnimationFrame || ((cb) => { cb && cb(Date.now()); return 1; });
globalThis.cancelAnimationFrame = globalThis.cancelAnimationFrame || (() => {});
globalThis.localStorage = globalThis.localStorage || { getItem: () => null, setItem: () => {}, removeItem: () => {}, clear: () => {} };
globalThis.sessionStorage = globalThis.sessionStorage || { getItem: () => null, setItem: () => {}, removeItem: () => {}, clear: () => {} };
globalThis.fetch = globalThis.fetch || (async () => ({ ok: true, status: 200, headers: { get: () => null }, json: async () => ({}), text: async () => '', arrayBuffer: async () => new ArrayBuffer(0) }));
globalThis.MutationObserver = globalThis.MutationObserver || function(){ this.observe=()=>{}; this.disconnect=()=>{}; this.takeRecords=()=>[]; };
globalThis.AbortController = globalThis.AbortController || function(){ this.signal={aborted:false,addEventListener:()=>{},removeEventListener:()=>{}}; this.abort=()=>{ this.signal.aborted=true; }; };
globalThis.crypto = globalThis.crypto || {};
globalThis.crypto.getRandomValues = globalThis.crypto.getRandomValues || ((arr) => { for (let i = 0; i < arr.length; i++) arr[i] = (Math.random() * 256) | 0; return arr; });
globalThis.TextEncoder = globalThis.TextEncoder || util.TextEncoder;
globalThis.TextDecoder = globalThis.TextDecoder || util.TextDecoder;

const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
let bundle = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/secure-teup0d-D6PE046x.js', 'utf8');
const exportIndex = bundle.lastIndexOf('export{');
if (exportIndex >= 0) bundle = bundle.slice(0, exportIndex);

try {
  eval(bundle);
} catch (e) {
  console.error('bundle eval failed', e);
  process.exit(1);
}

function asText(v) {
  if (v == null) return null;
  if (typeof v === 'string') return v;
  if (ArrayBuffer.isView(v)) return Buffer.from(v.buffer, v.byteOffset, v.byteLength).toString('latin1');
  if (v instanceof ArrayBuffer) return Buffer.from(new Uint8Array(v)).toString('latin1');
  if (typeof v.length === 'number') {
	const chars = [];
	for (let i = 0; i < v.length; i++) chars.push(String.fromCharCode((v[i] ?? 0) & 255));
	return chars.join('');
  }
  try { return JSON.stringify(v); } catch { return String(v); }
}

function sample(name, v) {
  const text = asText(v);
  const normalized = (() => {
	let cur = text || '';
	for (let i = 0; i < 4; i++) {
	  if (!cur.includes('%')) break;
	  try {
		const dec = decodeURIComponent(cur);
		if (dec === cur) break;
		cur = dec;
	  } catch { break; }
	}
	return cur;
  })();

  console.log(name, JSON.stringify({
	rawType: v == null ? 'null' : typeof v,
	textLen: text?.length ?? 0,
	textHead: (text ?? '').slice(0, 120),
	normHead: normalized.slice(0, 120),
	jsonHint: normalized.startsWith('{') && normalized.includes('"status"')
  }));
}

try {
  const r = globalThis.Ii?.R?.(oracle.e);
  const d = globalThis.Ii?.D ? globalThis.Ii.D(r) : null;
  const i = globalThis.Ii?.I ? globalThis.Ii.I(r) : null;
  sample('R', r);
  sample('D(R)', d);
  sample('I(R)', i);
} catch (e) {
  console.error('decrypt call failed', e);
}
