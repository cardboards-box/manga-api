// Capture 50+ tokens in one session and immediately solve for current masks
'use strict';
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  const tokens = new Map(); // id -> token string

  page.on('request', req => {
	const url = req.url();
	// Capture chapter-list requests: /api/v1/manga/{id}/chapters?...&_=TOKEN
	const m = url.match(/\/api\/v1\/manga\/([^\/]+)\/chapters\?.*[&?]_=([\w\-]+)/);
	if (m) {
	  const id = m[1];
	  const tok = m[2];
	  if (!tokens.has(id)) {
		tokens.set(id, tok);
		process.stderr.write('captured ' + id + ' -> ' + tok.substring(0, 20) + '...\n');
	  }
	}
  });

  // Navigate to several pages to collect tokens
  const seedUrls = [
	'https://comix.to/en/title/55kwg',
	'https://comix.to/en/title/60jxz',
	'https://comix.to/en/title/8w6dm',
	'https://comix.to/en/title/n93ny',
	'https://comix.to/en/title/qqwrm',
	'https://comix.to/en/title/mr3m0',
	'https://comix.to/en/title/gxgm9',
	'https://comix.to/en/title/d0n78',
	'https://comix.to/en/title/ll172',
	'https://comix.to/en/title/xqz07',
	'https://comix.to/en/title/50l0g',
	'https://comix.to/en/title/2zxnk',
	'https://comix.to/en/title/w9k2g',
	'https://comix.to/en/title/yegj1',
	'https://comix.to/en/title/zgj83',
	'https://comix.to/en/title/eqy5m',
	'https://comix.to/en/title/7ldne',
	'https://comix.to/en/title/vw9zy',
	'https://comix.to/en/title/3rq32',
	'https://comix.to/en/title/w0ren',
	'https://comix.to/en/title/87dld',
	'https://comix.to/en/title/7n3xg',
	'https://comix.to/en/title/k9317',
	'https://comix.to/en/title/vw2lq',
	'https://comix.to/en/title/d19rg',
	'https://comix.to/en/title/j6126',
	'https://comix.to/en/title/exx6r',
	'https://comix.to/en/title/101vd',
	'https://comix.to/en/title/39jrg',
	'https://comix.to/en/title/m00yd',
	'https://comix.to/en/title/l6g12',
	'https://comix.to/en/title/1r05n',
	'https://comix.to/en/title/m8ky0',
	'https://comix.to/en/title/x9r6j',
	'https://comix.to/en/title/70w00',
	'https://comix.to/en/title/3rdr3',
	'https://comix.to/en/title/9wz0j',
	'https://comix.to/en/title/6exl0',
	'https://comix.to/en/title/ydq0v',
	'https://comix.to/en/title/q9gjd',
	'https://comix.to/en/title/nk9re',
	'https://comix.to/en/title/e071e',
	'https://comix.to/en/title/qd5kd',
	'https://comix.to/en/title/9dmm0',
	'https://comix.to/en/title/dkgd8',
	'https://comix.to/en/title/w0wm8',
	'https://comix.to/en/title/lgkg7',
	'https://comix.to/en/title/zdm9j',
	'https://comix.to/en/title/dx5jr',
	'https://comix.to/en/title/lldl2',
  ];

  for (const url of seedUrls) {
	if (tokens.size >= 50) break;
	try {
	  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30000 });
	  await page.waitForTimeout(2500);
	} catch(e) {
	  process.stderr.write('nav error ' + url + ': ' + e.message + '\n');
	}
  }

  await browser.close();

  const oracles = [...tokens.entries()].map(([id, tok]) => [id, tok]);
  process.stderr.write('\nCaptured ' + oracles.length + ' tokens\n');

  if (oracles.length < 40) {
	console.error('Not enough tokens to solve (need >=40, got ' + oracles.length + ')');
	process.exit(1);
  }

  // Solve
  function fromB64url(s) {
	return [...Buffer.from(s.replace(/-/g,'+').replace(/_/g,'/'), 'base64')];
  }
  function buildFB(id, n) {
	const bits = [];
	for (let i=0;i<n;i++){const ch=i<id.length?id.charCodeAt(i):0;for(let b=0;b<8;b++)bits.push((ch>>b)&1);}
	return bits;
  }
  function maskBitsToNum(bits){let n=0n;for(let i=0;i<bits.length;i++)if(bits[i])n|=1n<<BigInt(i);return n;}
  function solvGF2(A,b_col){
	const m=A.length,n=A[0].length;
	const mat=A.map((row,i)=>[...row,b_col[i]]);
	const pc=[];let row=0;
	for(let col=0;col<n&&row<m;col++){
	  let pr=-1;for(let r=row;r<m;r++)if(mat[r][col]===1){pr=r;break;}
	  if(pr<0)continue;
	  [mat[row],mat[pr]]=[mat[pr],mat[row]];pc.push({row,col});
	  for(let r=0;r<m;r++){if(r!==row&&mat[r][col]===1)for(let c=col;c<=n;c++)mat[r][c]^=mat[row][c];}
	  row++;
	}
	for(let r=row;r<m;r++)if(mat[r][n]===1)return null;
	const x=new Array(n).fill(0);for(const{row:r,col:c}of pc)x[c]=mat[r][n];return x;
  }

  const allB = oracles.map(([,tok])=>fromB64url(tok));
  const pfx = allB[0].slice(0,49);
  const sfx = allB[0].slice(54);

  // Verify all share same prefix/suffix
  let ok=true;
  for(const b of allB){
	for(let i=0;i<49;i++)if(b[i]!==pfx[i]){ok=false;break;}
	for(let i=0;i<11;i++)if(b[54+i]!==sfx[i]){ok=false;break;}
  }
  if(!ok){console.error('MIXED SESSIONS - prefix/suffix mismatch!');process.exit(1);}
  console.error('Prefix/suffix consistent across all tokens');

  const A = oracles.map(([id])=>buildFB(id,5));
  const masks=[];
  let inc=0;
  for(let ob=0;ob<40;ob++){
	const byteIdx=49+(ob>>3),bitIdx=ob&7;
	const b_col=allB.map(bytes=>(bytes[byteIdx]>>bitIdx)&1);
	const sol=solvGF2(A,b_col);
	if(!sol){inc++;masks.push(0n);}
	else masks.push(maskBitsToNum(sol));
  }
  console.error('Inconsistent bits: '+inc);

  const pfxBuf=Buffer.from(pfx),sfxBuf=Buffer.from(sfx);
  let pass=0,fail=0;
  for(const[id,expected]of oracles){
	const bytes=Buffer.alloc(pfxBuf.length+5+sfxBuf.length);
	pfxBuf.copy(bytes);
	const fb=buildFB(id,5);
	for(let ob=0;ob<40;ob++){
	  let p=0;for(let b=0;b<fb.length;b++)if(fb[b]&&((masks[ob]>>BigInt(b))&1n))p^=1;
	  if(p)bytes[49+(ob>>3)]|=(1<<(ob&7));
	}
	sfxBuf.copy(bytes,54);
	const got=bytes.toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
	if(got===expected)pass++;
	else{fail++;console.error('FAIL '+id);}
  }
  console.error(`Verification: ${pass}/${oracles.length} pass, ${fail} fail`);

  if(pass===oracles.length){
	console.log('// Prefix');
	console.log(JSON.stringify(pfx));
	console.log('// Suffix');
	console.log(JSON.stringify(sfx));
	console.log('// Masks (40 elements)');
	for(let i=0;i<40;i+=8)
	  console.log(masks.slice(i,i+8).map(n=>n.toString()).join(', '));
  } else {
	console.error('FAILED - masks underdetermined or mixed sessions');
	process.exit(1);
  }
})();
