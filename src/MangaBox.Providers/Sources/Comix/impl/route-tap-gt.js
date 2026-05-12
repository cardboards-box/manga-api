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
	const inject = `
var __mbxTapLog=[];
var __mbxOrigGt=gt;
gt=function(QC,Qi,Qx,QR,Qf,QD){
  try{
	var rec={
	  qcType:typeof QC,
	  qcCtor:QC&&QC.constructor?QC.constructor.name:null,
	  qcArr2Len:QC&&Array.isArray(QC[2])?QC[2].length:null,
	  qc8Type:QC?typeof QC[8]:null,
	  qc8Len:QC&&typeof QC[8]==='string'?QC[8].length:null,
	  qc2Type:QC?typeof QC[2]:null,
	  qc1:QC&&typeof QC[1]==='number'?QC[1]:null,
	  qc13:QC&&typeof QC[13]==='number'?QC[13]:null,
	  qc17:QC&&typeof QC[17]==='number'?QC[17]:null,
	  qcOwnKeys:QC?Object.keys(QC).slice(0,20):null,
	  qiType:typeof Qi,
	  qiLen:Qi&&typeof Qi.length==='number'?Qi.length:null,
	  qiHead:Array.isArray(Qi)?Qi.slice(0,6):null,
	  qxType:typeof Qx,
	  qxLen:Qx&&typeof Qx.length==='number'?Qx.length:null,
	  qxOwnKeys:Qx&&typeof Qx==='object'?Object.keys(Qx).slice(0,20):null,
	  qrType:typeof QR,
	  qfType:typeof Qf,
	  qdType:typeof QD
	};
	__mbxTapLog.push(rec);
	if(__mbxTapLog.length>120)__mbxTapLog.shift();
  }catch(e){}
  return __mbxOrigGt(QC,Qi,Qx,QR,Qf,QD);
};
var ge=function(QC,Qi,Qx,QR,Qf,QD){return gt(QC,Qi,Qx,QR,0,QD)};
globalThis.__mbxTap={getLog:function(){return __mbxTapLog.slice();}, clear:function(){__mbxTapLog=[];}};
`;

	if (body.includes(anchor)) {
	  body = body.replace(anchor, inject);
	}

	await route.fulfill({ status: response.status(), headers: response.headers(), body });
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(9000);

  const result = await page.evaluate(async () => {
	const out = { hasTap: !!window.__mbxTap, logCount: 0, sample: null };

	if (!window.__mbxTap) return out;

	// force a direct decrypt call as well
	const req = await fetch('/api/v1/manga/8w6dm/chapters?page=1&limit=20&order%5Bnumber%5D=desc&_=YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
	const json = await req.json();
	try { window.Ii.R(json.e); } catch {}

	const log = window.__mbxTap.getLog();
	out.logCount = log.length;
	out.sample = log;
	return out;
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/gt-tap-log.json', JSON.stringify(result, null, 2));
  console.log(JSON.stringify({ hasTap: result.hasTap, logCount: result.logCount }));

  await browser.close();
})();
