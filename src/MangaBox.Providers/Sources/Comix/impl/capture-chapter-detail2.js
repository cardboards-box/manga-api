// Capture chapter detail request URL with direct navigation
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('request', (req) => {
	const u = req.url();
	if (u.includes('/api/v1/chapters/')) {
	  console.log('CHAPTER-DETAIL:', u);
	}
	if (u.includes('/api/v1/manga/') && u.includes('/chapters')) {
	  console.log('CHAPTER-LIST:', u);
	}
  });

  // Try a manga with known chapters
  await page.goto('https://comix.to/title/60jxz', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  const hrefs = await page.$$eval('a', els => 
	els.map(e => e.href).filter(h => h.includes('/chapter/'))
  );
  console.log('Chapter hrefs found:', hrefs.slice(0,3));

  if (hrefs.length > 0) {
	await page.goto(hrefs[0], { waitUntil: 'domcontentloaded', timeout: 60000 });
	await page.waitForTimeout(5000);
  } else {
	// Try navigating to a known chapter URL directly
	await page.goto('https://comix.to', { waitUntil: 'domcontentloaded', timeout: 60000 });
	await page.waitForTimeout(3000);

	// Find any chapter link on site
	const allHrefs = await page.$$eval('a', els => 
	  els.map(e => e.href).filter(h => h.includes('/chapter/') || h.includes('/read/'))
	);
	console.log('All chapter-like hrefs:', allHrefs.slice(0, 5));

	if (allHrefs.length > 0) {
	  await page.goto(allHrefs[0], { waitUntil: 'domcontentloaded', timeout: 60000 });
	  await page.waitForTimeout(5000);
	}
  }

  await browser.close();
})();
