// Try to navigate directly to a chapter URL for comix.to
// Looking at the URLs we've captured, let me use the Comix API to find a chapter
const { chromium } = require('playwright');
const https = require('https');

(async () => {
  // First get a chapter ID via the API
  const token = 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqRqdFOGf_F4zgSnZ-rqG4Y';
  const url = `https://comix.to/api/v1/manga/60jxz/chapters?page=1&limit=5&order%5Bnumber%5D=desc&_=${token}`;

  const data = await new Promise((resolve, reject) => {
	https.get(url, {
	  headers: {
		'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
		'Accept': 'application/json',
		'Origin': 'https://comix.to',
		'Referer': 'https://comix.to/'
	  }
	}, (res) => {
	  let body = '';
	  res.on('data', d => body += d);
	  res.on('end', () => resolve({ status: res.statusCode, body }));
	}).on('error', reject);
  });

  console.log('Status:', data.status);
  console.log('Body:', data.body.substring(0, 500));

  if (data.status === 200) {
	try {
	  const json = JSON.parse(data.body);
	  const chapters = json?.result?.items || json?.result || [];
	  console.log('Chapters found:', chapters.length);
	  if (chapters.length > 0) {
		const firstChapter = chapters[0];
		console.log('First chapter:', JSON.stringify(firstChapter).substring(0, 200));
		const chapterId = firstChapter.id || firstChapter.hashId;
		if (chapterId) {
		  console.log('\nChapter ID:', chapterId);

		  // Now capture the chapter detail URL via browser
		  const browser = await chromium.launch({ headless: true });
		  const page = await browser.newPage();

		  page.on('request', (req) => {
			const u = req.url();
			if (u.includes('/api/v1/chapters/')) {
			  console.log('CHAPTER-DETAIL:', u);
			}
		  });

		  await page.goto(`https://comix.to/chapter/${chapterId}`, { waitUntil: 'domcontentloaded', timeout: 60000 });
		  await page.waitForTimeout(5000);

		  await browser.close();
		}
	  }
	} catch(e) {
	  console.log('JSON parse error:', e.message);
	}
  }
})();
