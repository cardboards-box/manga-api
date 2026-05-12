const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', msg => {
	const t = msg.text();
	if (t.startsWith('II_PROXY')) console.log(t);
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(6000);

  const out = await page.evaluate(async () => {
	const req = await fetch('/api/v1/manga/8w6dm/chapters?page=1&limit=20&order%5Bnumber%5D=desc&_=YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y');
	const json = await req.json();
	const e = json.e;

	const Ii = window.Ii;
	const src = Ii.R.toString();

	// Build percent-decoded byte array expected at stage output.
	const final = Ii.R(e);
	const encoded = encodeURIComponent(final);
	const targetBytes = Array.from(encoded, c => c.charCodeAt(0));

	// Try to identify helper call names in source by simple regex of Ii.<name>(...)
	const helperNames = [...new Set(Array.from(src.matchAll(/Ii\.([A-Za-z_$][A-Za-z0-9_$]*)\(/g)).map(m => m[1]))];

	const helperInfo = {};
	for (const n of helperNames) {
	  try {
		const fn = Ii[n];
		helperInfo[n] = { type: typeof fn, len: typeof fn === 'function' ? fn.length : null, src: typeof fn === 'function' ? fn.toString().slice(0, 200) : null };
	  } catch {}
	}

	return {
	  rSrcHead: src.slice(0, 1000),
	  helperNames,
	  helperInfo,
	  targetLen: targetBytes.length,
	  targetHead: targetBytes.slice(0, 40),
	  finalHead: final.slice(0, 120),
	};
  });

  fs.writeFileSync('MangaBox.Providers/Sources/Comix/impl/ii-r-source-probe.json', JSON.stringify(out));
  console.log('II_PROXY done helpers', out.helperNames);

  await browser.close();
})();
