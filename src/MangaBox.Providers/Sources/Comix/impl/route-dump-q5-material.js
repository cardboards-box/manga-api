const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));

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

	const geAnchor = 'var ge=function(QC,Qi,Qx,QR,Qf,QD){return gt(QC,Qi,Qx,QR,0,QD)};';
	if (body.includes(geAnchor)) {
	  body = body.replace(geAnchor, `${geAnchor}globalThis.__mbxExpose={gt:gt,ge:ge,vmx:vmx_26c226};globalThis.__mbxExposeHit=1;`);
	}

	const q5Anchor = 'let Q5=function(QC,Qi,Qx){let QR=X;X=null;let Qf=l,';
	if (body.includes(q5Anchor)) {
	  body = body.replace(
		q5Anchor,
		'let Q5=function(QC,Qi,Qx){let QR=X;X=null;let Qf=l;try{globalThis.__mbxQ5={hasX:!!QR,hasL:!!Qf,xType:typeof QR,lType:typeof Qf,xLen:Array.isArray(QR)?QR.length:null,lLen:Array.isArray(Qf)?Qf.length:null,xHead:Array.isArray(QR)?QR.slice(0,20):null,lHead:Array.isArray(Qf)?Qf.slice(0,20):null};}catch(_e){};'
	  );
	}

	await route.fulfill({
	  status: response.status(),
	  headers: response.headers(),
	  body,
	});
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(5000);

  const result = await page.evaluate((enc) => {
	const out = {
	  hasIi: !!window.Ii,
	  q5: window.__mbxQ5 || null,
	  decryptLen: 0,
	  decryptHead: null,
	  err: null,
	};

	try {
	  const dec = window.Ii.R(enc);
	  out.decryptLen = dec.length;
	  out.decryptHead = dec.slice(0, 120);
	} catch (e) {
	  out.err = String(e?.message || e);
	}

	out.q5 = window.__mbxQ5 || out.q5;
	return out;
  }, oracle.e);

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/q5-material.json', JSON.stringify(result, null, 2));
  console.log(JSON.stringify({ hasIi: result.hasIi, hasQ5: !!result.q5, xLen: result.q5?.xLen ?? null, lLen: result.q5?.lLen ?? null, decryptLen: result.decryptLen, err: result.err }));

  await browser.close();
})();
