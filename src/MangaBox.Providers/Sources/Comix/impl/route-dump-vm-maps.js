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
	  body = body.replace(anchor, `${anchor}globalThis.__mbxExpose={gt:gt,ge:ge,vmx:vmx_26c226};globalThis.__mbxExposeHit=1;`);
	}

	await route.fulfill({ status: response.status(), headers: response.headers(), body });
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(7000);

  const data = await page.evaluate(() => {
	const vmx = globalThis.__mbxExpose?.vmx;
	if (!vmx) return { error: 'no-vmx' };

	const out = { maps: {} };

	for (const key of ['_$65Xto2', '_$W64rRR']) {
	  const m = vmx[key];
	  if (!(m instanceof Map)) {
		out.maps[key] = { type: typeof m, isMap: false };
		continue;
	  }

	  const entries = [];
	  let count = 0;
	  for (const [k, v] of m.entries()) {
		if (count >= 200) break;
		entries.push({
		  keyType: typeof k,
		  keyName: typeof k === 'function' ? (k.name || '(anon)') : null,
		  keyLen: typeof k === 'function' ? k.length : null,
		  valueType: typeof v,
		  valueCtor: v && v.constructor ? v.constructor.name : null,
		  valueKeys: v && typeof v === 'object' ? Object.keys(v).slice(0, 20) : null,
		});
		count++;
	  }

	  out.maps[key] = { isMap: true, size: m.size, sample: entries };
	}

	return out;
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/vm-maps-dump.json', JSON.stringify(data, null, 2));
  console.log(JSON.stringify({ ok: !data.error, keys: Object.keys(data.maps || {}) }));

  await browser.close();
})();
