const pairs = [
  ['8w6dm','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y'],
  ['60jxz','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqRqdFOGf_F4zgSnZ-rqG4Y'],
  ['55kym','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y'],
  ['ll172','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatIGx8uVf_F4zgSnZ-rqG4Y'],
  ['5r7m','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEapkkCXnvO5CB39rUUwyzOw'],
  ['mr3m0','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatEkiXlVf_F4zgSnZ-rqG4Y'],
  ['qqwrm','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeat0rEZrof_F4zgSnZ-rqG4Y'],
  ['793e','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqOTiXjvO5CB39rUUwyzOw'],
  ['2zxnk','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEapgssBqof_F4zgSnZ-rqG4Y'],
  ['80d0m','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZqLjLof_F4zgSnZ-rqG4Y'],
  ['lw2j','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatIpaZnvO5CB39rUUwyzOw'],
  ['n8we','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatySEXjvO5CB39rUUwyzOw'],
  ['lldl2','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatIGLtqVf_F4zgSnZ-rqG4Y'],
  ['zdm9j','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauA-TwuIf_F4zgSnZ-rqG4Y'],
  ['gxgm9','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatNSD3luf_F4zgSnZ-rqG4Y'],
  ['l7re','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatJpcXjvO5CB39rUUwyzOw'],
  ['keyz','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeat8_0JPvO5CB39rUUwyzOw'],
  ['qd5kd','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeat0-SDnHf_F4zgSnZ-rqG4Y'],
  ['nr83','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatwkqJLvO5CB39rUUwyzOw'],
  ['7n3xg','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqMgiVMnf_F4zgSnZ-rqG4Y'],
  ['jx9n3','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatBSyBq1f_F4zgSnZ-rqG4Y'],
  ['nk9re','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEeatw9yJrnf_F4zgSnZ-rqG4Y'],
];

const decode = (t) => {
  const s = t.replace(/-/g, '+').replace(/_/g, '/');
  const p = s + '==='.slice((s.length + 3) % 4);
  return Buffer.from(p, 'base64');
};

const features = (id) => {
  const f = [];
  for (let i = 0; i < 5; i++) {
	const ch = i < id.length ? id.charCodeAt(i) & 0xFF : 0;
	for (let b = 0; b < 8; b++) f.push((ch >> b) & 1);
  }
  f.push(1); // bias
  return f;
};

function solveBit(rows, ys, cols) {
  const a = rows.map((r, i) => [...r, ys[i]]);
  let row = 0;
  const pivots = [];
  for (let col = 0; col < cols && row < a.length; col++) {
	let sel = row;
	while (sel < a.length && a[sel][col] === 0) sel++;
	if (sel === a.length) continue;
	[a[row], a[sel]] = [a[sel], a[row]];
	pivots.push(col);
	for (let r = 0; r < a.length; r++) {
	  if (r !== row && a[r][col]) {
		for (let c = col; c <= cols; c++) a[r][c] ^= a[row][c];
	  }
	}
	row++;
  }

  for (let r = row; r < a.length; r++) {
	if (a[r][cols]) throw new Error('No solution');
  }

  const x = Array(cols).fill(0);
  for (let r = pivots.length - 1; r >= 0; r--) {
	const col = pivots[r];
	let v = a[r][cols];
	for (let c = col + 1; c < cols; c++) v ^= (a[r][c] & x[c]);
	x[col] = v;
  }
  return x;
}

const rows = pairs.map(([id]) => features(id));
const vars = rows[0].length;

const solutions = [];
for (let outBit = 0; outBit < 40; outBit++) {
  const byte = 49 + (outBit >> 3);
  const bit = outBit & 7;
  const ys = pairs.map(([, tok]) => (decode(tok)[byte] >> bit) & 1);
  solutions.push(solveBit(rows, ys, vars));
}

const test = (id) => {
  const f = features(id);
  const out = Array(5).fill(0);
  for (let ob = 0; ob < 40; ob++) {
	let v = 0;
	const s = solutions[ob];
	for (let i = 0; i < vars; i++) v ^= (s[i] & f[i]);
	if (v) out[ob >> 3] |= 1 << (ob & 7);
  }
  return out;
};

let ok = 0;
for (const [id, tok] of pairs) {
  const b = decode(tok);
  const got = test(id);
  const exp = [...b.slice(49, 54)];
  const same = got.every((v, i) => v === exp[i]);
  if (same) ok++;
}

console.log('fit', ok, '/', pairs.length);

for (let ob = 0; ob < 40; ob++) {
  const s = solutions[ob];
  let lo = 0n;
  let hi = 0;
  for (let i = 0; i < 40; i++) {
	if (!s[i]) continue;
	if (i < 32) lo |= 1n << BigInt(i);
	else hi |= 1 << (i - 32);
  }
  const bias = s[40] ? 1 : 0;
  console.log(ob, lo.toString(), hi, bias);
}

const sample = decode(pairs[0][1]);
console.log('prefix49', [...sample.slice(0,49)]);
console.log('const54_64', [...sample.slice(54,65)]);
