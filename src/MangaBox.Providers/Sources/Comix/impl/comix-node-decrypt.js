const fs = require('fs');
const path = require('path');

let payload = process.argv[2] || '';
if (!payload) {
  try {
    payload = fs.readFileSync(0, 'utf8').trim();
  } catch {}
}
if (!payload) {
  process.stdout.write(JSON.stringify({ ok: false, error: 'missing-payload' }));
  process.exit(0);
}

function bytesToText(bytes) {
  let out = '';
  for (let i = 0; i < bytes.length; i++) out += String.fromCharCode(bytes[i] & 255);
  return out;
}

function asText(v) {
  if (v == null) return null;
  if (typeof v === 'string') return v;

  try {
	if (typeof ArrayBuffer !== 'undefined') {
	  if (typeof ArrayBuffer.isView === 'function' && ArrayBuffer.isView(v)) {
		return bytesToText(v);
	  }
	  if (v instanceof ArrayBuffer) {
		return bytesToText(new Uint8Array(v));
	  }
	}
  } catch {}

  try {
	if (typeof v.length === 'number' && v.length >= 0) {
	  let out = '';
	  for (let i = 0; i < v.length; i++) {
		const n = v[i];
		if (typeof n === 'number') out += String.fromCharCode(n & 255);
		else if (typeof n === 'string' && n.length > 0) out += n.charAt(0);
		else return null;
	  }
	  return out;
	}
  } catch {}

  try {
	if (typeof v === 'object') return JSON.stringify(v);
  } catch {}

  try { return String(v); } catch { return null; }
}

function normalizeText(text) {
  if (typeof text !== 'string') return null;
  let current = text.trim();

  for (let i = 0; i < 5; i++) {
	if (!current || current.indexOf('%') < 0) break;
	try {
	  const decoded = decodeURIComponent(current);
	  if (decoded === current) break;
	  current = decoded;
	} catch {
	  break;
	}
  }

  if (current.length > 1 && current.charCodeAt(0) === 34 && current.charCodeAt(current.length - 1) === 34) {
	try {
	  const unwrapped = JSON.parse(current);
	  if (typeof unwrapped === 'string') current = unwrapped;
	} catch {}
  }

  return current;
}

function looksLikeJson(text) {
  if (typeof text !== 'string') return false;
  const t = text.trim();
  if (!t) return false;
  if (t.charCodeAt(0) === 123 || t.charCodeAt(0) === 91) return true;
  if (t.indexOf('%7B') === 0 || t.indexOf('%5B') === 0) return true;
  if (t.indexOf('%22status%22') >= 0) return true;
  return false;
}

function looksLikeBase64Layer(text) {
  if (typeof text !== 'string') return false;
  if (text.length < 80) return false;
  for (let i = 0; i < text.length; i++) {
	const ch = text.charCodeAt(i);
	const isUpper = ch >= 65 && ch <= 90;
	const isLower = ch >= 97 && ch <= 122;
	const isNum = ch >= 48 && ch <= 57;
	if (!(isUpper || isLower || isNum || ch === 95 || ch === 45 || ch === 43 || ch === 47 || ch === 61)) return false;
  }
  return true;
}

function scoreCandidate(text) {
  if (typeof text !== 'string' || text.length === 0) return Number.NEGATIVE_INFINITY;

  let score = 0;
  if (text.indexOf('%22status%22') >= 0) score += 1200;
  if (text.indexOf('%7B') >= 0 || text.indexOf('%5B') >= 0) score += 700;
  if (text.startsWith('%7B') || text.startsWith('%5B')) score += 400;
  if (looksLikeBase64Layer(text)) score += 150;

  const decoded = normalizeText(tryDecodeBase64ToText(text));
  if (decoded) {
	if (looksLikeJson(decoded)) score += 1600;
	if (decoded.indexOf('"status"') >= 0) score += 900;
  }

  score -= Math.floor(Math.min(text.length, 200000) / 256);
  return score;
}

function keyOf(v) {
  const text = asText(v);
  if (!text) return null;
  return `${text.length}:${text.slice(0, 120)}`;
}

function tryDecodeBase64ToText(layer) {
  if (!looksLikeBase64Layer(layer)) return null;
  try {
    const b64 = layer.replace(/-/g, '+').replace(/_/g, '/');
    const pad = '='.repeat((4 - (b64.length % 4)) % 4);
    const buf = Buffer.from(b64 + pad, 'base64');
    if (!buf || buf.length === 0) return null;
    const utf8 = buf.toString('utf8').trim();
    if (utf8) return utf8;
  } catch {}
  return null;
}

try {
  globalThis.globalThis = globalThis;
  globalThis.window = globalThis;
  globalThis.self = globalThis;
  globalThis.document = globalThis.document || {
	currentScript: null,
	createElement: () => ({}),
	addEventListener: () => {},
	removeEventListener: () => {},
  };

  try {
	Object.defineProperty(globalThis, 'navigator', {
	  value: { appCodeName: 'Mozilla', userAgent: 'Mozilla/5.0', platform: 'Win32', language: 'en-US' },
	  configurable: true,
	  writable: true,
	});
  } catch {
	globalThis.navigator = globalThis.navigator || {};
	globalThis.navigator.appCodeName = globalThis.navigator.appCodeName || 'Mozilla';
  }

  globalThis.location = globalThis.location || { href: 'https://comix.to/', host: 'comix.to', origin: 'https://comix.to' };
  globalThis.performance = globalThis.performance || { now: () => Date.now() };
  globalThis.window.addEventListener = globalThis.window.addEventListener || (() => {});
  globalThis.window.removeEventListener = globalThis.window.removeEventListener || (() => {});
  globalThis.window.dispatchEvent = globalThis.window.dispatchEvent || (() => true);
  globalThis.__vmTaskQueue = globalThis.__vmTaskQueue || [];

  const scheduleTask = (cb) => {
	if (typeof cb === 'function') globalThis.__vmTaskQueue.push(cb);
  };

  globalThis.queueMicrotask = (cb) => scheduleTask(cb);
  globalThis.setTimeout = (cb) => { scheduleTask(cb); return globalThis.__vmTaskQueue.length; };
  globalThis.clearTimeout = () => {};
  globalThis.setInterval = (cb) => { scheduleTask(cb); return globalThis.__vmTaskQueue.length; };
  globalThis.clearInterval = () => {};
  globalThis.requestAnimationFrame = (cb) => { scheduleTask(cb); return globalThis.__vmTaskQueue.length; };
  globalThis.cancelAnimationFrame = () => {};

  if (typeof globalThis.atob !== 'function') {
	globalThis.atob = function(input) {
	  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';
	  const str = String(input).replace(/=+$/, '');
	  let output = '';
	  for (let bc = 0, bs, buffer, idx = 0; (buffer = str.charAt(idx++)); ~buffer && (bs = bc % 4 ? bs * 64 + buffer : buffer, bc++ % 4)
		? output += String.fromCharCode(255 & bs >> (-2 * bc & 6)) : 0) {
		buffer = chars.indexOf(buffer);
	  }
	  return output;
	};
  }

  if (typeof globalThis.btoa !== 'function') {
	globalThis.btoa = function(input) {
	  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';
	  const str = String(input);
	  let output = '';
	  for (let block, charCode, idx = 0, map = chars; str.charAt(idx | 0) || (map = '=', idx % 1);
		output += map.charAt(63 & block >> 8 - idx % 1 * 8)) {
		charCode = str.charCodeAt(idx += 3 / 4);
		if (charCode > 255) throw new Error('btoa range error');
		block = block << 8 | charCode;
	  }
	  return output;
	};
  }

  if (!globalThis.crypto) globalThis.crypto = {};
  if (typeof globalThis.crypto.getRandomValues !== 'function') {
	globalThis.crypto.getRandomValues = function(arr) {
	  for (let i = 0; i < arr.length; i++) arr[i] = (Math.random() * 256) | 0;
	  return arr;
	};
  }

  if (typeof globalThis.TextEncoder !== 'function') {
	globalThis.TextEncoder = function() {};
	globalThis.TextEncoder.prototype.encode = function(s) {
	  const str = unescape(encodeURIComponent(String(s)));
	  const out = new Uint8Array(str.length);
	  for (let i = 0; i < str.length; i++) out[i] = str.charCodeAt(i);
	  return out;
	};
  }

  if (typeof globalThis.TextDecoder !== 'function') {
	globalThis.TextDecoder = function() {};
	globalThis.TextDecoder.prototype.decode = function(bytes) {
	  let str = '';
	  for (let i = 0; i < bytes.length; i++) str += String.fromCharCode(bytes[i]);
	  try { return decodeURIComponent(escape(str)); } catch { return str; }
	};
  }

  globalThis.document.addEventListener = globalThis.document.addEventListener || (() => {});
  globalThis.document.removeEventListener = globalThis.document.removeEventListener || (() => {});
  globalThis.document.dispatchEvent = globalThis.document.dispatchEvent || (() => true);
  globalThis.document.querySelector = globalThis.document.querySelector || (() => null);
  globalThis.document.querySelectorAll = globalThis.document.querySelectorAll || (() => []);
  globalThis.window.document = globalThis.document;
  globalThis.history = globalThis.history || { pushState: () => {}, replaceState: () => {}, state: null };
  globalThis.screen = globalThis.screen || { width: 1920, height: 1080 };
  globalThis.localStorage = globalThis.localStorage || { getItem: () => null, setItem: () => {}, removeItem: () => {}, clear: () => {} };
  globalThis.sessionStorage = globalThis.sessionStorage || { getItem: () => null, setItem: () => {}, removeItem: () => {}, clear: () => {} };

  globalThis.fetch = globalThis.fetch || (() => Promise.resolve({
	ok: true,
	status: 200,
	headers: { get: () => null },
	json: () => Promise.resolve({}),
	text: () => Promise.resolve(''),
	arrayBuffer: () => Promise.resolve(new ArrayBuffer(0)),
  }));

  if (typeof globalThis.Headers !== 'function') {
	globalThis.Headers = function() {
	  this.get = () => null;
	  this.set = () => {};
	  this.append = () => {};
	};
  }

  if (typeof globalThis.Request !== 'function') {
	globalThis.Request = function(url, init) {
	  this.url = url;
	  this.method = init && init.method ? init.method : 'GET';
	  this.headers = init && init.headers ? init.headers : new Headers();
	};
  }

  if (typeof globalThis.Response !== 'function') {
	globalThis.Response = function(body) {
	  this.ok = true;
	  this.status = 200;
	  this.headers = new Headers();
	  this.text = () => Promise.resolve(String(body || ''));
	  this.json = () => Promise.resolve({});
	};
  }

  if (typeof globalThis.MutationObserver !== 'function') {
	globalThis.MutationObserver = function() {
	  this.observe = () => {};
	  this.disconnect = () => {};
	  this.takeRecords = () => [];
	};
  }

  if (typeof globalThis.AbortController !== 'function') {
	globalThis.AbortController = function() {
	  this.signal = { aborted: false, addEventListener: () => {}, removeEventListener: () => {} };
	  this.abort = () => { this.signal.aborted = true; };
	};
  }

  function runPendingTasks(maxRounds) {
	if (!globalThis.__vmTaskQueue || !globalThis.__vmTaskQueue.length) return;
	const rounds = typeof maxRounds === 'number' ? maxRounds : 3;
	for (let r = 0; r < rounds; r++) {
	  if (!globalThis.__vmTaskQueue.length) break;
	  const tasks = globalThis.__vmTaskQueue.slice(0);
	  globalThis.__vmTaskQueue.length = 0;
	  for (let t = 0; t < tasks.length; t++) {
		try { tasks[t](); } catch {}
	  }
	}
  }

  const bundlePath = path.join(__dirname, 'secure-teup0d-D6PE046x.js');
  let bundle = fs.readFileSync(bundlePath, 'utf8');
  const exportIndex = bundle.lastIndexOf('export{');
  if (exportIndex >= 0) bundle = bundle.slice(0, exportIndex);

  try { eval(bundle); } catch {}

  if (typeof Ii === 'undefined' || !Ii) {
	process.stdout.write(JSON.stringify({ ok: false, error: '__vm_no_ii__' }));
	process.exit(0);
  }

  const ops = [];
  if (typeof Ii.R === 'function') ops.push(Ii.R);
  if (typeof Ii.D === 'function') ops.push(Ii.D);
  if (typeof Ii.I === 'function') ops.push(Ii.I);
  if (ops.length === 0) {
	process.stdout.write(JSON.stringify({ ok: false, error: '__vm_no_ops__' }));
	process.exit(0);
  }

  runPendingTasks(6);

  const namedOps = {
	R: typeof Ii.R === 'function' ? Ii.R : null,
	D: typeof Ii.D === 'function' ? Ii.D : null,
	I: typeof Ii.I === 'function' ? Ii.I : null,
  };

  const preferredOrder = ['D', 'R', 'I'];
  const opNames = preferredOrder.filter(k => typeof namedOps[k] === 'function');

  function applyTextOp(name, input) {
	const fn = namedOps[name];
	if (!fn) return null;
	let out;
	try { out = fn(input); } catch { return null; }
	if (out == null) return null;
	const text = normalizeText(asText(out));
	return typeof text === 'string' && text.length > 0 ? text : null;
  }

  const seenCandidateSet = new Set();
  const pathByCandidate = new Map();
  const nextCandidates = [];
  let fallbackText = null;
  let fallbackPath = null;

  function considerCandidate(text, path) {
	if (!text || typeof text !== 'string') return;
	if (!pathByCandidate.has(text)) pathByCandidate.set(text, path);

	if (looksLikeJson(text)) {
	  process.stdout.write(JSON.stringify({ ok: true, json: text, path: path.join('>') }));
	  process.exit(0);
	}

	if (looksLikeBase64Layer(text)) {
	  if (!seenCandidateSet.has(text)) {
		seenCandidateSet.add(text);
		nextCandidates.push(text);
	  }

	  const decoded = normalizeText(tryDecodeBase64ToText(text));
	  if (decoded && decoded.length > 0) {
		const decodedPath = [...path, 'B64'];
		if (!pathByCandidate.has(decoded)) pathByCandidate.set(decoded, decodedPath);
		if (looksLikeJson(decoded)) {
		  process.stdout.write(JSON.stringify({ ok: true, json: decoded, path: decodedPath.join('>') }));
		  process.exit(0);
		}
		if (looksLikeBase64Layer(decoded) && !seenCandidateSet.has(decoded)) {
		  seenCandidateSet.add(decoded);
		  nextCandidates.push(decoded);
		}
	  }
	} else if (!fallbackText) {
	  fallbackText = text;
	  fallbackPath = path;
	}
  }

  const deterministicPaths = [
	['D'],
	['R'],
	['D', 'R'],
	['R', 'D'],
	['D', 'R', 'D'],
	['D', 'R', 'R'],
	['R', 'D', 'R']
  ].filter(path => path.every(p => opNames.includes(p)));

  for (const p of deterministicPaths) {
	let current = payload;
	let failed = false;
	const prefix = [];
	for (const opName of p) {
	  prefix.push(opName);
	  current = applyTextOp(opName, current);
	  if (!current) {
		failed = true;
		break;
	  }
	  considerCandidate(current, [...prefix]);
	}
	if (!failed && current) {
	  considerCandidate(current, [...prefix]);
	}
  }

  if (nextCandidates.length > 0) {
	const unique = [...new Set(nextCandidates)];
	unique.sort((a, b) => {
	  const delta = scoreCandidate(b) - scoreCandidate(a);
	  if (delta !== 0) return delta;
	  return a.length - b.length;
	});
	const best = unique[0];
	const bestPath = pathByCandidate.has(best) ? pathByCandidate.get(best).join('>') : '';
	process.stdout.write(JSON.stringify({ ok: false, next: best, candidates: unique, path: bestPath }));
	process.exit(0);
  }

  if (fallbackText) {
	process.stdout.write(JSON.stringify({ ok: false, next: fallbackText, path: fallbackPath ? fallbackPath.join('>') : '' }));
	process.exit(0);
  }

  process.stdout.write(JSON.stringify({ ok: false, error: '__vm_no_candidate__' }));
} catch (err) {
  process.stdout.write(JSON.stringify({ ok: false, error: String(err && err.message ? err.message : err) }));
}
