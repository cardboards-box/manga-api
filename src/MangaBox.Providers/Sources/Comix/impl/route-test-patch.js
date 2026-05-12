const { chromium } = require('playwright');

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

	console.log('PATCH target', url, 'len', body.length);
	const had = body.includes('var ge=function(QC,Qi,Qx,QR,Qf,QD){return gt(QC,Qi,Qx,QR,0,QD)};');
	console.log('PATCH anchor', had);

	body += '\n;globalThis.__routePatchHit=1;';

	await route.fulfill({
	  status: response.status(),
	  headers: response.headers(),
	  body,
	});
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(6000);

  const check = await page.evaluate(() => ({
	patch: globalThis.__routePatchHit || 0,
	hasIi: !!globalThis.Ii,
	hasV: typeof globalThis.v,
  }));

  console.log(check);
  await browser.close();
})();
