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
	  body = body.replace(
		anchor,
		`${anchor}globalThis.__mbxExpose={gt:gt,ge:ge,vmx:vmx_26c226};globalThis.__mbxExposeHit=1;`
	  );
	}

	await route.fulfill({
	  status: response.status(),
	  headers: response.headers(),
	  body,
	});
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(6000);

  const out = await page.evaluate(() => {
	const result = {
	  hasIi: !!window.Ii,
	  hasExpose: !!window.__mbxExpose,
	  hasVmx: !!window.__mbxExpose?.vmx,
	  vmxKeys: window.__mbxExpose?.vmx ? Object.keys(window.__mbxExpose.vmx).slice(0, 40) : [],
	  keyMaterial: null,
	  keyError: null,
	};

	try {
	  const vmx = window.__mbxExpose?.vmx;
	  if (!vmx) {
		result.keyError = 'vmx missing';
		return result;
	  }

	  const keyFnName = '_$ZYBOT9';
	  const keyFn = vmx[keyFnName];
	  if (typeof keyFn !== 'function') {
		result.keyError = `missing function ${keyFnName}`;
		return result;
	  }

	  const arr = keyFn();
	  result.keyMaterial = {
		type: typeof arr,
		isArray: Array.isArray(arr),
		length: arr?.length ?? null,
		first: Array.isArray(arr) ? arr.slice(0, 24) : null,
	  };
	} catch (e) {
	  result.keyError = String(e?.message || e);
	}

	return result;
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/live-vm-key-material.json', JSON.stringify(out, null, 2));
  console.log(JSON.stringify({ hasIi: out.hasIi, hasExpose: out.hasExpose, hasVmx: out.hasVmx, keyError: out.keyError, keyLen: out.keyMaterial?.length ?? null }));

  await browser.close();
})();
