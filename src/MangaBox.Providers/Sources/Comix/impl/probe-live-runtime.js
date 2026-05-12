const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', msg => {
	const t = msg.text();
	if (t.startsWith('PROBE_')) console.log(t);
  });

  await page.addInitScript(() => {
	const origFromCharCodeApply = String.fromCharCode.apply.bind(String.fromCharCode);
	String.fromCharCode.apply = function (thisArg, arr) {
	  if (arr && typeof arr.length === 'number' && arr.length >= 8900 && arr.length <= 9050) {
		try {
		  console.log('PROBE_FCA ' + JSON.stringify({ len: arr.length, first: Array.from(arr.slice(0, 24)) }));
		} catch {
		}
	  }
	  return origFromCharCodeApply(thisArg, arr);
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const info = await page.evaluate(async () => {
	const out = { hasV: typeof window.v === 'function', hasGi: typeof window.Gi === 'function' };

	const payload = await (async () => {
	  const req = await fetch('/api/v1/manga/8w6dm/chapters?page=1&limit=20&order%5Bnumber%5D=desc&_=YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
	  const json = await req.json();
	  return json?.e || null;
	})();

	out.eLen = payload ? payload.length : -1;

	if (typeof window.v === 'function' && payload) {
	  try {
		const r1 = window.v(payload);
		out.vType = typeof r1;
		out.vCtor = r1 && r1.constructor ? r1.constructor.name : null;
		out.vHead = typeof r1 === 'string' ? r1.slice(0, 120) : null;
	  } catch (e) {
		out.vErr = String(e && e.message ? e.message : e);
	  }
	}

	if (typeof window.Gi === 'function' && payload) {
	  try {
		const r2 = window.Gi(payload);
		out.giType = typeof r2;
		out.giCtor = r2 && r2.constructor ? r2.constructor.name : null;
		out.giHead = typeof r2 === 'string' ? r2.slice(0, 120) : null;
	  } catch (e) {
		out.giErr = String(e && e.message ? e.message : e);
	  }
	}

	return out;
  });

  console.log('PROBE_INFO ' + JSON.stringify(info));

  await browser.close();
})();
