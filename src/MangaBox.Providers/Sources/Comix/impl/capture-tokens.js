const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const tokens = new Map();

  page.on('request', (req) => {
	const url = req.url();
	const m = url.match(/\/api\/v1\/manga\/([^/]+)\/chapters\?[^#]*[?&]_=(.+)$/);
	if (m && !tokens.has(m[1])) {
	  tokens.set(m[1], m[2]);
	  console.log(`${m[1]} ${m[2]}`);
	}
  });

  const slugs = [
	'8w6dm-solo-leveling',
	'60jxz',
	'0l96q-jujutsu-kaisen',
	'1w7qz-one-piece',
	'6xv18-chainsaw-man',
	'wv53d-one-punch-man',
	'5j9ly-berserk',
	'k34p8-kaiju-no-8',
	'k4lq7-sakamoto-days',
	'l07w5-dandadan'
  ];

  for (const slug of slugs) {
	const url = `https://comix.to/title/${slug}`;
	try {
	  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
	  await page.waitForTimeout(6000);
	} catch (e) {
	  console.log(`ERR ${slug}`);
	}
  }

  await browser.close();
  console.log(`COUNT ${tokens.size}`);
})();
