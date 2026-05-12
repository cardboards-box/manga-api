const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', (msg) => {
	const text = msg.text();
	if (text.startsWith('TRACE_')) {
	  console.log(text);
	}
  });

  await page.addInitScript(() => {
	const trace = (type, payload) => {
	  try {
		console.log(`TRACE_${type} ` + JSON.stringify(payload));
	  } catch {
	  }
	};

	const origAtob = window.atob.bind(window);
	window.atob = function (s) {
	  const out = origAtob(s);
	  if (out.length > 500) {
		trace('ATOB', {
		  inLen: s.length,
		  outLen: out.length,
		  inHead: s.slice(0, 48),
		  outHeadHex: Array.from(out.slice(0, 16), c => c.charCodeAt(0).toString(16).padStart(2, '0')).join('')
		});
	  }
	  return out;
	};

	const origEncode = TextEncoder.prototype.encode;
	TextEncoder.prototype.encode = function (input) {
	  const out = origEncode.call(this, input);
	  if (typeof input === 'string' && input.length > 500) {
		trace('ENCODE', {
		  inLen: input.length,
		  outLen: out.length,
		  inHead: input.slice(0, 48)
		});
	  }
	  return out;
	};

	const origDecode = TextDecoder.prototype.decode;
	TextDecoder.prototype.decode = function (...args) {
	  const out = origDecode.apply(this, args);
	  if (typeof out === 'string' && out.length > 500) {
		trace('DECODE', {
		  outLen: out.length,
		  outHead: out.slice(0, 80)
		});
	  }
	  return out;
	};

	const origFromCharCodeApply = String.fromCharCode.apply.bind(String.fromCharCode);
	String.fromCharCode.apply = function (thisArg, arr) {
	  if (arr && typeof arr.length === 'number' && arr.length > 500) {
		try {
		  const first = [];
		  for (let i = 0; i < Math.min(16, arr.length); i++) first.push(arr[i]);
		  trace('FROMCHAR', { len: arr.length, first });
		} catch {
		}
	  }
	  return origFromCharCodeApply(thisArg, arr);
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(14000);
  await browser.close();
})();
