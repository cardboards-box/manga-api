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

	console.log('PATCH target', url, 'len', body.length);

	const anchor = 'var ge=function(QC,Qi,Qx,QR,Qf,QD){return gt(QC,Qi,Qx,QR,0,QD)};';
	const has = body.includes(anchor);
	console.log('PATCH anchor', has);

	if (has) {
	  body = body.replace(
		anchor,
		`${anchor}globalThis.__mbxExpose={gt:gt,ge:ge,vmx:vmx_26c226};globalThis.__mbxExposeHit=1;`
	  );
	}

	body += '\n;globalThis.__routePatchHit=1;';

	await route.fulfill({
	  status: response.status(),
	  headers: response.headers(),
	  body,
	});
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(6000);

  const info = await page.evaluate(() => {
	const out = {
	  routeHit: window.__routePatchHit || 0,
	  hit: window.__mbxExposeHit || 0,
	  hasExpose: !!window.__mbxExpose,
	  hasIi: !!window.Ii,
	  iiKeys: window.Ii ? Object.keys(window.Ii) : [],
	};

	if (window.__mbxExpose?.vmx) {
	  out.vmxKeys = Object.keys(window.__mbxExpose.vmx).slice(0, 30);
	  out.hasMap = !!window.__mbxExpose.vmx._$65Xto2;
	}

	if (window.__mbxExpose?.vmx?._$65Xto2 && window.Ii?.R) {
	  try {
		const m = window.__mbxExpose.vmx._$65Xto2.get(window.Ii.R);
		out.rMetaType = typeof m;
		out.rMeta = m;
	  } catch (e) {
		out.rMetaErr = String(e?.message || e);
	  }
	}

	return out;
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/live-metadata.json', JSON.stringify(info, null, 2));
  console.log(JSON.stringify(info));

  await browser.close();
})();
