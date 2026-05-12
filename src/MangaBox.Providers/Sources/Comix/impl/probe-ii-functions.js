const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const out = await page.evaluate(async () => {
	const req = await fetch('/api/v1/manga/8w6dm/chapters?page=1&limit=20&order%5Bnumber%5D=desc&_=YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
	const j = await req.json();
	const e = j.e;

	const res = {};
	const ii = window.Ii;

	for (const name of ['O', 'I', 'D', 'R']) {
	  const fn = ii?.[name];
	  if (typeof fn !== 'function') {
		res[name] = { ok: false, reason: 'not-fn' };
		continue;
	  }

	  const cases = [];
	  const argsList = [
		[e],
		[e, null],
		[e, {}],
		[e, window],
		[e, { request: {}, response: {}, config: {} }],
	  ];

	  for (const args of argsList) {
		try {
		  const r = fn.apply(ii, args);
		  cases.push({
			argsLen: args.length,
			ok: true,
			type: typeof r,
			ctor: r && r.constructor ? r.constructor.name : null,
			isNull: r === null,
			isUndef: r === undefined,
			head: typeof r === 'string' ? r.slice(0, 120) : null,
		  });
		} catch (err) {
		  cases.push({ argsLen: args.length, ok: false, err: String(err?.message ?? err) });
		}
	  }

	  res[name] = { len: fn.length, src: fn.toString().slice(0, 220), cases };
	}

	return res;
  });

  console.log(JSON.stringify(out));
  await browser.close();
})();
