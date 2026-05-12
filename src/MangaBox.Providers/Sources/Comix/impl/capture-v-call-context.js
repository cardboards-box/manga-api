const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', msg => {
	const text = msg.text();
	if (text.startsWith('V_CALL')) console.log(text);
  });

  await page.addInitScript(() => {
	const wrap = () => {
	  if (typeof window.v !== 'function' || window.__vWrapped) return;
	  const orig = window.v;
	  window.__vWrapped = true;
	  window.v = function (...args) {
		try {
		  const arg0 = args[0];
		  const len = typeof arg0 === 'string' ? arg0.length : null;
		  const thisKeys = this && typeof this === 'object' ? Object.keys(this).slice(0, 20) : [];
		  const thisCtor = this && this.constructor ? this.constructor.name : null;
		  console.log('V_CALL ' + JSON.stringify({
			argc: args.length,
			a0Type: typeof arg0,
			a0Len: len,
			thisCtor,
			thisKeys,
			hasRequest: !!(this && this.request),
			hasResponse: !!(this && this.response),
			hasConfig: !!(this && this.config),
		  }));
		} catch {
		}
		return orig.apply(this, args);
	  };
	};

	wrap();
	const timer = setInterval(() => {
	  wrap();
	  if (window.__vWrapped) clearInterval(timer);
	}, 50);
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(10000);
  await browser.close();
})();
