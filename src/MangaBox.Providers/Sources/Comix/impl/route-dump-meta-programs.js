const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.route('**/*.js', async (route) => {
	const url = route.request().url();
	if (!url.includes('secure-teup0d-')) {
	  await route.continue();
	  return;
	}

	const response = await route.fetch();
	let body = await response.text();

	const anchor = 'var ge=function(QC,Qi,Qx,QR,Qf,QD){return gt(QC,Qi,Qx,QR,0,QD)};';
	if (body.includes(anchor)) {
	  body = body.replace(anchor, `${anchor}globalThis.__mbxExpose={vmx:vmx_26c226};`);
	}

	await route.fulfill({ status: response.status(), headers: response.headers(), body });
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(7000);

  const data = await page.evaluate(() => {
	const map = globalThis.__mbxExpose?.vmx?._$65Xto2;
	const root = globalThis.Ii?.R;
	if (!map || !root) return { error: 'missing' };

	const seen = new Set();

	function summarizeMeta(meta, depth = 0) {
	  if (!meta || typeof meta !== 'object') return null;
	  const out = {};
	  for (const k of Reflect.ownKeys(meta)) {
		const key = String(k);
		const v = meta[k];

		if (typeof v === 'function') {
		  out[key] = {
			type: 'function',
			name: v.name || '(anon)',
			len: v.length,
		  };
		} else if (Array.isArray(v)) {
		  out[key] = {
			type: 'array',
			len: v.length,
			head: v.slice(0, 20),
		  };
		} else if (v && typeof v === 'object') {
		  const own = Reflect.ownKeys(v).map(String);
		  out[key] = {
			type: 'object',
			ctor: v.constructor ? v.constructor.name : null,
			ownKeys: own.slice(0, 20),
			maybeProgram: Array.isArray(v[2]) && typeof v[8] === 'string',
			arr2Len: Array.isArray(v[2]) ? v[2].length : null,
			str8Len: typeof v[8] === 'string' ? v[8].length : null,
			num13: typeof v[13] === 'number' ? v[13] : null,
			num1: typeof v[1] === 'number' ? v[1] : null,
			num17: typeof v[17] === 'number' ? v[17] : null,
		  };
		} else {
		  out[key] = { type: typeof v, value: v };
		}
	  }
	  return out;
	}

	const chain = [];
	let fn = root;

	for (let depth = 0; depth < 8 && typeof fn === 'function' && !seen.has(fn); depth++) {
	  seen.add(fn);
	  const meta = map.get(fn);

	  chain.push({
		depth,
		fnName: fn.name || '(anon)',
		fnLen: fn.length,
		fnSrc: fn.toString().slice(0, 200),
		metaSummary: summarizeMeta(meta),
	  });

	  if (meta && typeof meta === 'object' && typeof meta.I === 'function') {
		fn = meta.I;
	  } else {
		break;
	  }
	}

	return { chain };
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/meta-programs.json', JSON.stringify(data, null, 2));
  console.log(JSON.stringify({ ok: !data.error, depth: data.chain ? data.chain.length : 0 }));

  await browser.close();
})();
