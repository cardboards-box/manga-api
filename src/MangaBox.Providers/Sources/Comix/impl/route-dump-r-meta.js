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

  const data = await page.evaluate(async () => {
	const out = {};

	const map = globalThis.__mbxExpose?.vmx?._$65Xto2;
	if (!map) {
	  out.error = 'no-map';
	  return out;
	}

	const r = globalThis.Ii?.R;
	out.hasR = typeof r === 'function';

	if (!r) return out;

	const meta = map.get(r);
	out.metaType = typeof meta;
	out.metaOwnKeys = meta ? Reflect.ownKeys(meta).map(String) : [];

	if (meta) {
	  out.metaProto = Object.getPrototypeOf(meta)?.constructor?.name ?? null;
	  out.metaEntries = [];
	  for (const k of Reflect.ownKeys(meta)) {
		const v = meta[k];
		out.metaEntries.push({
		  key: String(k),
		  type: typeof v,
		  ctor: v && v.constructor ? v.constructor.name : null,
		  isArray: Array.isArray(v),
		  len: (Array.isArray(v) || ArrayBuffer.isView(v) || typeof v === 'string') ? v.length : null,
		});
	  }
	}

	return out;
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/r-meta-dump.json', JSON.stringify(data, null, 2));
  console.log(JSON.stringify(data));

  await browser.close();
})();
