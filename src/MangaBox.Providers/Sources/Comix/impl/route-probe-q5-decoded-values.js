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

	const qxAnchor = 'let QX=QW+A[""],Ql=Q0(QL),QA=new Uint8Array(Ql.length);';
	if (body.includes(qxAnchor)) {
	  body = body.replace(
		qxAnchor,
		'let QX=QW+A[""],Ql=Q0(QL),QA=new Uint8Array(Ql.length);try{var __mbxQ5=globalThis.__mbxQ5||(globalThis.__mbxQ5={raw:[],decoded:[]});if(__mbxQ5.raw.length<80)__mbxQ5.raw.push({idx:Qe,qlLen:Ql.length,encLen:QL.length,encHead:QL.slice(0,32),qxLen:QX.length,qxHead:QX.slice(0,16)});}catch(_e){};'
	  );
	}

	const setAnchor = 'Qt[Qe]=Qx(QA)}else Qt[Qe]=QL;';
	if (body.includes(setAnchor)) {
	  body = body.replace(
		setAnchor,
		'Qt[Qe]=Qx(QA);try{var __v=Qt[Qe];var __d={idx:Qe,t:typeof __v,isArr:Array.isArray(__v),arrLen:Array.isArray(__v)?__v.length:null,strLen:typeof __v==="string"?__v.length:null,strHead:typeof __v==="string"?__v.slice(0,120):null,objKeys:__v&&typeof __v==="object"?Object.keys(__v).slice(0,20):null,qaLen:QA.length,qaHead:Array.from(QA.slice(0,24))};var __mbxQ5=globalThis.__mbxQ5||(globalThis.__mbxQ5={raw:[],decoded:[]});if(__mbxQ5.decoded.length<80)__mbxQ5.decoded.push(__d);}catch(_e){};}else Qt[Qe]=QL;'
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
	  decryptLen: 0,
	  err: null,
	  q5RawCount: (window.__mbxQ5?.raw || []).length,
	  q5DecodedCount: (window.__mbxQ5?.decoded || []).length,
	  q5Raw: (window.__mbxQ5?.raw || []).slice(0, 60),
	  q5Decoded: (window.__mbxQ5?.decoded || []).slice(0, 60),
	};

	try {
	  const dec = window.Ii?.R ? window.Ii.R(enc) : null;
	  if (typeof dec === 'string') {
		out.decryptLen = dec.length;
	  }
	} catch (e) {
	  out.err = String(e?.message || e);
	}

	out.q5RawCount = (window.__mbxQ5?.raw || []).length;
	out.q5DecodedCount = (window.__mbxQ5?.decoded || []).length;
	out.q5Raw = (window.__mbxQ5?.raw || []).slice(0, 60);
	out.q5Decoded = (window.__mbxQ5?.decoded || []).slice(0, 60);
	return out;
  }, oracle.e);

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/q5-decoded-values.json', JSON.stringify(result, null, 2));
  console.log(JSON.stringify({ hasIi: result.hasIi, decryptLen: result.decryptLen, raw: result.q5RawCount, decoded: result.q5DecodedCount, err: result.err }));

  await browser.close();
})();
