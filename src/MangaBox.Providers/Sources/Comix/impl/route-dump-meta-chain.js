const { chromium } = require('playwright');

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

  const info = await page.evaluate(() => {
	const map = globalThis.__mbxExpose?.vmx?._$65Xto2;
	if (!map) return { error: 'no-map' };

	const seen = new WeakSet();

	const summarize = (value, depth = 0) => {
	  if (value === null || value === undefined) return { type: typeof value };
	  const t = typeof value;
	  if (t !== 'object' && t !== 'function') return { type: t, value: value };
	  if (seen.has(value)) return { type: t, cycle: true };
	  seen.add(value);

	  const out = {
		type: t,
		ctor: value.constructor ? value.constructor.name : null,
	  };

	  if (t === 'function') {
		out.name = value.name;
		out.length = value.length;
	  }

	  if (depth >= 3) return out;

	  const meta = map.get(value);
	  if (meta !== undefined) {
		out.metaType = typeof meta;
		out.metaCtor = meta && meta.constructor ? meta.constructor.name : null;

		if (meta && (typeof meta === 'object' || typeof meta === 'function')) {
		  out.metaKeys = Reflect.ownKeys(meta).map(String).slice(0, 20);
		  out.metaProps = {};
		  for (const k of Reflect.ownKeys(meta).slice(0, 12)) {
			const ks = String(k);
			try {
			  out.metaProps[ks] = summarize(meta[k], depth + 1);
			} catch (e) {
			  out.metaProps[ks] = { error: String(e?.message || e) };
			}
		  }
		}
	  }

	  if (t === 'object') {
		out.keys = Reflect.ownKeys(value).map(String).slice(0, 20);
	  }

	  return out;
	};

	return {
	  R: summarize(globalThis.Ii?.R),
	  D: summarize(globalThis.Ii?.D),
	  I: summarize(globalThis.Ii?.I),
	  O: summarize(globalThis.Ii?.O),
	  v: summarize(globalThis.v),
	  Gi: summarize(globalThis.Gi),
	};
  });

  console.log(JSON.stringify(info));
  await browser.close();
})();
