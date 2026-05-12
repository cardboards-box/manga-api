const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const data = await page.evaluate(async () => {
	const req = await fetch('/api/v1/manga/8w6dm/chapters?page=1&limit=20&order%5Bnumber%5D=desc&_=YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
	const json = await req.json();
	const e = json?.e;

	const calls = [];
	const orig = String.fromCharCode.apply.bind(String.fromCharCode);
	String.fromCharCode.apply = function (thisArg, arr) {
	  if (arr && typeof arr.length === 'number' && arr.length > 500) {
		calls.push(Array.from(arr));
	  }
	  return orig(thisArg, arr);
	};

	let output = null;
	let err = null;
	try {
	  output = window.Ii.D(e);
	} catch (ex) {
	  err = String(ex?.message ?? ex);
	}

	String.fromCharCode.apply = orig;

	return {
	  e,
	  eLen: e?.length ?? -1,
	  outputHead: typeof output === 'string' ? output.slice(0, 180) : null,
	  outputLen: typeof output === 'string' ? output.length : -1,
	  error: err,
	  callLengths: calls.map(c => c.length),
	  calls,
	};
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/ii-d-oracle.json', JSON.stringify(data));
  console.log('captured', data.callLengths);

  await browser.close();
})();
