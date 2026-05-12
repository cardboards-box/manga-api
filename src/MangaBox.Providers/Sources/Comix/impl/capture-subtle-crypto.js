const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on('console', (msg) => {
	const text = msg.text();
	if (text.startsWith('SUBTLE_')) {
	  console.log(text);
	}
  });

  await page.addInitScript(() => {
	const toBytes = (value) => {
	  try {
		if (!value) return null;
		if (value instanceof ArrayBuffer) return new Uint8Array(value);
		if (ArrayBuffer.isView(value)) return new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
		return null;
	  } catch {
		return null;
	  }
	};

	const b64 = (bytes) => {
	  if (!bytes) return null;
	  let s = '';
	  for (let i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
	  return btoa(s);
	};

	const subtle = crypto?.subtle;
	if (!subtle) return;

	const origImportKey = subtle.importKey.bind(subtle);
	subtle.importKey = async function (format, keyData, algorithm, extractable, keyUsages) {
	  try {
		const bytes = toBytes(keyData);
		console.log('SUBTLE_IMPORT ' + JSON.stringify({
		  format,
		  algorithm,
		  extractable,
		  keyUsages,
		  keyLen: bytes ? bytes.length : null,
		  keyB64: bytes && bytes.length <= 128 ? b64(bytes) : null
		}));
	  } catch {
	  }
	  return origImportKey(format, keyData, algorithm, extractable, keyUsages);
	};

	const origDecrypt = subtle.decrypt.bind(subtle);
	subtle.decrypt = async function (algorithm, key, data) {
	  try {
		const bytes = toBytes(data);
		let ivB64 = null;
		if (algorithm && typeof algorithm === 'object' && algorithm.iv) {
		  const iv = toBytes(algorithm.iv);
		  ivB64 = iv ? b64(iv) : null;
		}
		console.log('SUBTLE_DECRYPT ' + JSON.stringify({
		  algorithm,
		  dataLen: bytes ? bytes.length : null,
		  dataHeadB64: bytes ? b64(bytes.subarray(0, Math.min(64, bytes.length))) : null,
		  ivB64
		}));
	  } catch {
	  }

	  const out = await origDecrypt(algorithm, key, data);

	  try {
		const outBytes = toBytes(out);
		console.log('SUBTLE_DECRYPT_OUT ' + JSON.stringify({
		  outLen: outBytes ? outBytes.length : null,
		  outHeadB64: outBytes ? b64(outBytes.subarray(0, Math.min(128, outBytes.length))) : null
		}));
	  } catch {
	  }

	  return out;
	};
  });

  await page.goto('https://comix.to/title/8w6dm-solo-leveling', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForTimeout(15000);
  await browser.close();
})();
