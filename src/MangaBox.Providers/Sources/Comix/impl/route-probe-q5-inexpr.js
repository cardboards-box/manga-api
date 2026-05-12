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
	  body = body.replace(
		geAnchor,
		`${geAnchor}globalThis.__mbxExpose={gt:gt,ge:ge,vmx:vmx_26c226};globalThis.__mbxExposeHit=1;`
	  );
	}

	const qdAnchor = 'Qd=(()=>{try{return navigator.appCodeName}catch(Qa){return""}})();';
	if (body.includes(qdAnchor)) {
	  body = body.replace(
		qdAnchor,
		'Qd=(()=>{try{return navigator.appCodeName}catch(Qa){return""}})();try{globalThis.__mbxQ5Raw={xType:typeof QR,lType:typeof Qf,xLen:Array.isArray(QR)?QR.length:null,lLen:Array.isArray(Qf)?Qf.length:null,xHead:Array.isArray(QR)?QR.slice(0,32):null,lHead:Array.isArray(Qf)?Qf.slice(0,32):null};}catch(_e){};'
	  );
	}

	const qwAnchor = 'let QW=QD+Qd,Qt={};';
	if (body.includes(qwAnchor)) {
	  body = body.replace(
		qwAnchor,
		'let QW=QD+Qd,Qt={};try{globalThis.__mbxQ5Key={qdHex:QD,appCodeName:Qd,key:QW,keyLen:QW.length};}catch(_e){};'
	  );
	}

	const qlAnchor = 'let QL=QR[Qe];if("string"==typeof QL){';
	if (body.includes(qlAnchor)) {
	  body = body.replace(
		qlAnchor,
		'let QL=QR[Qe];if("string"==typeof QL){try{var __mbxQ5Dec=globalThis.__mbxQ5Dec||(globalThis.__mbxQ5Dec=[]);if(__mbxQ5Dec.length<40)__mbxQ5Dec.push({idx:Qe,len:QL.length,head:QL.slice(0,40)});}catch(_e){};'
	  );
	}

	await route.fulfill({
	  status: response.status(),
	  headers: response.headers(),
	  body,
	});
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(10000);

  const result = await page.evaluate((enc) => {
	const out = {
	  hasIi: !!window.Ii,
	  hasExpose: !!window.__mbxExpose,
	  q5Raw: window.__mbxQ5Raw || null,
	  q5Key: window.__mbxQ5Key || null,
	  q5DecCount: (window.__mbxQ5Dec || []).length,
	  q5Dec: (window.__mbxQ5Dec || []).slice(0, 20),
	  decryptLen: 0,
	  decryptHead: null,
	  err: null,
	};

	try {
	  const dec = window.Ii?.R ? window.Ii.R(enc) : null;
	  if (typeof dec === 'string') {
		out.decryptLen = dec.length;
		out.decryptHead = dec.slice(0, 140);
	  }
	} catch (e) {
	  out.err = String(e?.message || e);
	}

	out.q5Raw = window.__mbxQ5Raw || out.q5Raw;
	out.q5Key = window.__mbxQ5Key || out.q5Key;
	out.q5DecCount = (window.__mbxQ5Dec || []).length;
	out.q5Dec = (window.__mbxQ5Dec || []).slice(0, 20);
	return out;
  }, oracle.e);

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/q5-inexpr-probe.json', JSON.stringify(result, null, 2));
  console.log(JSON.stringify({
	hasIi: result.hasIi,
	hasExpose: result.hasExpose,
	xLen: result.q5Raw?.xLen ?? null,
	lLen: result.q5Raw?.lLen ?? null,
	keyLen: result.q5Key?.keyLen ?? null,
	decCount: result.q5DecCount,
	decryptLen: result.decryptLen,
	err: result.err,
  }));

  await browser.close();
})();
