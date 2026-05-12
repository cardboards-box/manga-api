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

	const out = {
	  vmxKeys: Object.keys(vmx),
	  vmxOwnProps: Reflect.ownKeys(vmx).map(String),
	  details: {},
	};

	for (const k of Reflect.ownKeys(vmx)) {
	  const key = String(k);
	  const v = vmx[k];
	  out.details[key] = {
		type: typeof v,
		ctor: v && v.constructor ? v.constructor.name : null,
		ownKeys: v && typeof v === 'object' ? Reflect.ownKeys(v).slice(0, 20).map(String) : null,
		isWeakMap: v instanceof WeakMap,
		isMap: v instanceof Map,
	  };
	}

	return out;
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/vm-props-dump.json', JSON.stringify(data, null, 2));
  console.log(JSON.stringify({ ok: !data.error, count: data.vmxOwnProps ? data.vmxOwnProps.length : 0 }));

  await browser.close();
})();
