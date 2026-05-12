const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const result = await page.evaluate(async () => {
	const req = await fetch('/api/v1/manga/8w6dm/chapters?page=1&limit=20&order%5Bnumber%5D=desc&_=YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
	const j = await req.json();
	const e = j.e;

	const matches = [];

	for (const name of Object.getOwnPropertyNames(window)) {
	  const fn = window[name];
	  if (typeof fn !== 'function') continue;

	  let src;
	  try {
		src = Function.prototype.toString.call(fn);
	  } catch {
		continue;
	  }

	  if (!src.includes('request')) continue;

	  try {
		let got = null;
		const callback = (...args) => {
		  got = args;
		  return args[0];
		};

		const maybe = fn(e, callback);

		matches.push({
		  name,
		  arity: fn.length,
		  returnedType: typeof maybe,
		  callbackCalled: !!got,
		  callbackArgTypes: got ? got.map(a => typeof a).slice(0, 5) : [],
		  callbackHead: got && typeof got[0] === 'string' ? got[0].slice(0, 120) : null,
		});
	  } catch {
	  }
	}

	return matches.slice(0, 50);
  });

  console.log(JSON.stringify(result));
  await browser.close();
})();
