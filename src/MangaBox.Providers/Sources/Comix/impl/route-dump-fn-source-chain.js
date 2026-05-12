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
	if (!map || !globalThis.Ii?.R) return { error: 'missing' };

	const root = globalThis.Ii.R;
	const visited = new Set();
	const out = [];

	function walk(fn, depth) {
	  if (typeof fn !== 'function') return;
	  if (visited.has(fn)) return;
	  visited.add(fn);

	  const src = Function.prototype.toString.call(fn);
	  const rec = {
		depth,
		name: fn.name || '(anon)',
		len: fn.length,
		srcHead: src.slice(0, 240),
	  };

	  const meta = map.get(fn);
	  if (meta && typeof meta === 'object') {
		rec.metaKeys = Reflect.ownKeys(meta).map(String);
	  } else {
		rec.metaKeys = null;
	  }

	  out.push(rec);

	  if (meta && typeof meta === 'object') {
		for (const k of Reflect.ownKeys(meta)) {
		  const v = meta[k];
		  if (typeof v === 'function') {
			walk(v, depth + 1);
		  }
		}
	  }
	}

	walk(root, 0);
	return { count: out.length, items: out };
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/fn-source-chain.json', JSON.stringify(data, null, 2));
  console.log(JSON.stringify({ count: data.count, error: data.error || null }));

  await browser.close();
})();
