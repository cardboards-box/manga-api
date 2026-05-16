// Enumerate all JS chunks to find signing bundle
const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  const chunks = [];
  page.on('response', async (resp) => {
	const url = resp.url();
	if (url.includes('/_next/static/') && url.endsWith('.js')) {
	  try {
		const text = await resp.text();
		chunks.push({ url, len: text.length });
		// Check for sign-related content
		if (text.includes('105991738') || text.includes('51780415') || text.includes('97,200,64')) {
		  console.log('SIGN_BUNDLE:', url);
		  fs.writeFileSync('live-sign-bundle.js', text);
		}
	  } catch(e) {}
	}
  });

  await page.goto('https://comix.to', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(3000);

  console.log('Total chunks:', chunks.length);
  chunks.sort((a,b)=>b.len-a.len);
  console.log('Largest 5:');
  for (const c of chunks.slice(0,5)) console.log(' ', c.len, c.url);

  await browser.close();
})();
