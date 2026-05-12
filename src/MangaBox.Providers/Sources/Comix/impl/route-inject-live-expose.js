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
	const inject = `${anchor}globalThis.__mbxExpose={gt:gt,ge:ge,gL:gL,vmx:vmx_26c226};globalThis.__mbxExposeHit=1;`;
	if (body.includes(anchor)) {
	  body = body.replace(anchor, inject);
	}

	await route.fulfill({
	  status: response.status(),
	  headers: response.headers(),
	  body,
	});
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(12000);

  const info = await page.evaluate(async () => {
	const out = {
	  exposeHit: window.__mbxExposeHit || 0,
	  hasExpose: !!window.__mbxExpose,
	  exposeKeys: window.__mbxExpose ? Object.keys(window.__mbxExpose) : [],
	  hasIi: !!window.Ii,
	  iiKeys: window.Ii ? Object.keys(window.Ii) : [],
	};

	if (window.__mbxExpose?.vmx) {
	  out.vmxKeys = Object.keys(window.__mbxExpose.vmx).slice(0, 40);
	  out.has65 = !!window.__mbxExpose.vmx._$65Xto2;
	  out.has7n = Object.prototype.hasOwnProperty.call(window.__mbxExpose.vmx, '_$7nAlNc');
	}

	if (window.__mbxExpose?.vmx?._$65Xto2 && window.Ii?.R) {
	  try {
		const m = window.__mbxExpose.vmx._$65Xto2.get(window.Ii.R);
		out.rMetaType = typeof m;
		out.rMetaKeys = m ? Object.keys(m) : null;
		out.rMetaPreview = m ? JSON.stringify(m).slice(0, 500) : null;
	  } catch (e) {
		out.rMetaErr = String(e?.message || e);
	  }
	}

	return out;
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/live-expose-info.json', JSON.stringify(info, null, 2));
  console.log(JSON.stringify(info));

  await browser.close();
})();
