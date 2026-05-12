const fs = require('fs');

const payload = process.argv[2] || JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json','utf8')).e;

globalThis.globalThis = globalThis;
globalThis.window = globalThis;
globalThis.self = globalThis;
globalThis.document = { currentScript: null, addEventListener(){}, removeEventListener(){}, createElement(){return{};} };
try {
  Object.defineProperty(globalThis, 'navigator', {
	value: { appCodeName: 'Mozilla', userAgent: 'Mozilla/5.0', platform:'Win32', language:'en-US' },
	configurable: true,
	writable: true,
  });
} catch {}
globalThis.location = { href: 'https://comix.to/', host: 'comix.to', origin: 'https://comix.to' };
globalThis.performance = { now: () => Date.now() };
globalThis.setTimeout = () => 1;
globalThis.setInterval = () => 1;
globalThis.queueMicrotask = () => {};
globalThis.requestAnimationFrame = () => 1;
globalThis.fetch = async () => ({ ok:true, headers:{ get:()=>null }, json: async()=>({}) });

let bundle = fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/secure-teup0d-D6PE046x.js','utf8');
const exportIndex = bundle.lastIndexOf('export{');
if (exportIndex >= 0) bundle = bundle.slice(0, exportIndex);
try { eval(bundle); } catch (e) { /* ignore late runtime throws */ }

if (!globalThis.Ii) {
  console.log('Ii missing');
  process.exit(0);
}

function asText(v) {
  if (v == null) return null;
  if (typeof v === 'string') return v;
  if (typeof v.length === 'number') {
	let out = '';
	for (let i = 0; i < v.length; i++) out += String.fromCharCode((v[i] ?? 0) & 255);
	return out;
  }
  try { return String(v); } catch { return null; }
}

function decodeLoop(text) {
  if (typeof text !== 'string') return '';
  let cur = text.trim();
  for (let i = 0; i < 5; i++) {
	if (!cur.includes('%')) break;
	try {
	  const dec = decodeURIComponent(cur);
	  if (dec === cur) break;
	  cur = dec;
	} catch {
	  break;
	}
  }
  return cur;
}

function isBase64Url(t) {
  return typeof t === 'string' && t.length > 60 && /^[A-Za-z0-9_-]+$/.test(t);
}

const ops = [];
if (typeof Ii.R === 'function') ops.push(['R', Ii.R]);
if (typeof Ii.D === 'function') ops.push(['D', Ii.D]);
if (typeof Ii.I === 'function') ops.push(['I', Ii.I]);

const seen = new Set();
let queue = [{ value: payload, path: 'e' }];
let best = null;

for (let depth = 0; depth < 5; depth++) {
  const next = [];
  for (const item of queue) {
	for (const [name, fn] of ops) {
	  let out;
	  try { out = fn(item.value); } catch { continue; }
	  const txt = asText(out);
	  if (!txt) continue;
	  const norm = decodeLoop(txt);
	  const key = `${norm.length}:${norm.slice(0,120)}`;
	  if (seen.has(key)) continue;
	  seen.add(key);

	  const hit = norm.startsWith('{') || norm.startsWith('[') || norm.includes('"status"') || norm.includes('%22status%22');
	  const b64 = isBase64Url(norm);

	  if (!best || (hit && !best.hit) || (!b64 && best.b64)) {
		best = { path: `${item.path}.${name}`, hit, b64, len: norm.length, head: norm.slice(0, 180) };
	  }

	  if (hit) {
		console.log('HIT', JSON.stringify({ path: `${item.path}.${name}`, len: norm.length, head: norm.slice(0, 200) }));
		process.exit(0);
	  }

	  next.push({ value: out, path: `${item.path}.${name}` });
	}
  }
  queue = next;
}

console.log('NO_HIT');
console.log(JSON.stringify(best, null, 2));
