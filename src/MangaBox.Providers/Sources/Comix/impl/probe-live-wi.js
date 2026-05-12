const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const result = await page.evaluate(async () => {
	const out = {};

	const req = await fetch('/api/v1/manga/8w6dm/chapters?page=1&limit=20&order%5Bnumber%5D=desc&_=YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
	const json = await req.json();
	const e = json?.e;
	out.eLen = e?.length ?? -1;

	if (typeof window.Wi === 'function' && e) {
	  try {
		const v = window.Wi(e);
		out.wiType = typeof v;
		out.wiCtor = v?.constructor?.name ?? null;
		out.wiHead = typeof v === 'string' ? v.slice(0, 160) : null;
	  } catch (err) {
		out.wiErr = String(err?.message ?? err);
	  }
	}

	if (typeof window.qi === 'function' && e) {
	  try {
		const v = window.qi(e);
		out.qiType = typeof v;
		out.qiCtor = v?.constructor?.name ?? null;
		out.qiHead = typeof v === 'string' ? v.slice(0, 160) : null;
	  } catch (err) {
		out.qiErr = String(err?.message ?? err);
	  }
	}

	return out;
  });

  console.log(JSON.stringify(result));
  await browser.close();
})();
