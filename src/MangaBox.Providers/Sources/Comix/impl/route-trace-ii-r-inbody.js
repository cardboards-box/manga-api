const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const oracle = JSON.parse(fs.readFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-oracle.json', 'utf8'));
  const e = oracle.e;

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

	const gtOpen = 'function gt(QC,Qi,Qx,QR,Qf,QD){';
	if (body.includes(gtOpen)) {
	  body = body.replace(
		gtOpen,
		`${gtOpen}var __mbxT=globalThis.__mbxTrace||(globalThis.__mbxTrace={calls:[]});var __mbxCall={qc17:QC&&QC[17],qc15:QC&&QC[15],qc13:QC&&QC[13],qc1:QC&&QC[1],qiLen:Qi&&typeof Qi.length==='number'?Qi.length:null,ops:[],qlHead:null};try{if(QC&&QC[2]&&QC[2].slice)__mbxCall.qlHead=QC[2].slice(0,120);}catch(_e){}__mbxT.calls.push(__mbxCall);if(__mbxT.calls.length>30)__mbxT.calls.shift();`
	  );
	}

	const opAnchor = 'let mL=QL[QG+(Qa<<Qj)]^QK;if(258===me){';
	if (body.includes(opAnchor)) {
	  body = body.replace(
		opAnchor,
		`let mL=QL[QG+(Qa<<Qj)]^QK;if(__mbxCall&&__mbxCall.ops&&__mbxCall.ops.length<1200){__mbxCall.ops.push([Qa,me,mL,QW]);}if(258===me){`
	  );
	}

	const geAnchor = 'var ge=function(QC,Qi,Qx,QR,Qf,QD){return gt(QC,Qi,Qx,QR,0,QD)};';
	if (body.includes(geAnchor)) {
	  body = body.replace(geAnchor, `${geAnchor}globalThis.__mbxTraceHit=1;`);
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
	  traceHit: window.__mbxTraceHit || 0,
	  hasIi: !!window.Ii,
	  hasTrace: !!window.__mbxTrace,
	  callCount: 0,
	  calls: [],
	  decryptHead: null,
	  decryptLen: 0,
	  err: null,
	};

	try {
	  const dec = window.Ii.R(enc);
	  out.decryptHead = dec.slice(0, 140);
	  out.decryptLen = dec.length;
	} catch (e) {
	  out.err = String(e?.message || e);
	}

	const calls = window.__mbxTrace?.calls || [];
	out.callCount = calls.length;
	out.calls = calls.slice(-10);
	return out;
  }, e);

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-inbody-trace.json', JSON.stringify(result, null, 2));
  console.log(JSON.stringify({ traceHit: result.traceHit, hasIi: result.hasIi, callCount: result.callCount, decryptLen: result.decryptLen }));

  await browser.close();
})();
