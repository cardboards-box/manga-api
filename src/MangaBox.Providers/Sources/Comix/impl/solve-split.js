'use strict';

function fromB64url(s) {
  return [...Buffer.from(s.replace(/-/g,'+').replace(/_/g,'/'), 'base64')];
}

function buildFB(id, n) {
  const bits = [];
  for (let i = 0; i < n; i++) {
    const ch = i < id.length ? id.charCodeAt(i) : 0;
    for (let b = 0; b < 8; b++) bits.push((ch >> b) & 1);
  }
  return bits;
}

function maskBitsToNum(bits) {
  let n = 0n;
  for (let i = 0; i < bits.length; i++) if (bits[i]) n |= 1n << BigInt(i);
  return n;
}

function solvGF2(A, b_col) {
  const m = A.length, n = A[0].length;
  const mat = A.map((row, i) => [...row, b_col[i]]);
  const pivotCol = [];
  let row = 0;
  for (let col = 0; col < n && row < m; col++) {
    let pr = -1;
    for (let r = row; r < m; r++) if (mat[r][col] === 1) { pr = r; break; }
    if (pr < 0) continue;
    [mat[row], mat[pr]] = [mat[pr], mat[row]];
    pivotCol.push({ row, col });
    for (let r = 0; r < m; r++) {
      if (r !== row && mat[r][col] === 1)
        for (let c = col; c <= n; c++) mat[r][c] ^= mat[row][c];
    }
    row++;
  }
  for (let r = row; r < m; r++) if (mat[r][n] === 1) return null;
  const x = new Array(n).fill(0);
  for (const { row: r, col: c } of pivotCol) x[c] = mat[r][n];
  return x;
}

function solveAndVerify(name, oracles) {
  const allBytes = oracles.map(([, tok]) => fromB64url(tok));
  const A = oracles.map(([id]) => buildFB(id, 5));
  const masks = [];
  let inconsistent = 0;
  for (let ob = 0; ob < 40; ob++) {
    const byteIdx = 49 + (ob >> 3), bitIdx = ob & 7;
    const b_col = allBytes.map(bytes => (bytes[byteIdx] >> bitIdx) & 1);
    const sol = solvGF2(A, b_col);
    if (!sol) { inconsistent++; masks.push(0n); }
    else masks.push(maskBitsToNum(sol));
  }
  const prefix49 = allBytes[0].slice(0, 49);
  const suffix11 = allBytes[0].slice(54);
  const pfxBuf = Buffer.from(prefix49);
  const sfxBuf = Buffer.from(suffix11);
  let pass = 0;
  for (const [id, expected] of oracles) {
    const bytes = Buffer.alloc(pfxBuf.length + 5 + sfxBuf.length);
    pfxBuf.copy(bytes);
    const fb = buildFB(id, 5);
    for (let ob = 0; ob < 40; ob++) {
      let p = 0;
      for (let b = 0; b < fb.length; b++) if (fb[b] && ((masks[ob] >> BigInt(b)) & 1n)) p ^= 1;
      if (p) bytes[49 + (ob >> 3)] |= (1 << (ob & 7));
    }
    sfxBuf.copy(bytes, 54);
    const got = bytes.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    if (got === expected) pass++;
  }
  console.log(name + ': inconsistent bits=' + inconsistent + ', pass=' + pass + '/' + oracles.length);
  if (pass === oracles.length) {
    console.log('  Prefix:', JSON.stringify(prefix49));
    console.log('  Suffix:', JSON.stringify(suffix11));
    console.log('  Masks:');
    for (let i = 0; i < 40; i += 8)
      console.log('    ' + masks.slice(i, i+8).map(n=>n.toString()).join(', ') + ',');
  }
  return { pass, masks, prefix49, suffix11 };
}

const batchA = [
  ['70w00','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqNqETJVf_F4zgSnZ-rqG4Y'],
  ['3rq32','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaockz5KVf_F4zgSnZ-rqG4Y'],
  ['w0ren','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauNqcXgIf_F4zgSnZ-rqG4Y'],
  ['vw9zy','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauQpyJNmf_F4zgSnZ-rqG4Y'],
  ['3rdr3','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaockLpq1f_F4zgSnZ-rqG4Y'],
  ['87dld','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZpLtrHf_F4zgSnZ-rqG4Y'],
  ['7n3xg','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqMgiVMnf_F4zgSnZ-rqG4Y'],
  ['d19rg','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopryJonf_F4zgSnZ-rqG4Y'],
  ['k9317','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat-TiVI1f_F4zgSnZ-rqG4Y'],
  ['vw2lq','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauQpadptf_F4zgSnZ-rqG4Y'],
  ['7ldne','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqMGLhrnf_F4zgSnZ-rqG4Y'],
  ['eqy5m','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaokr0NLof_F4zgSnZ-rqG4Y'],
  ['yegj1','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauU_D5l1f_F4zgSnZ-rqG4Y'],
  ['w9k2g','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauOTlHInf_F4zgSnZ-rqG4Y'],
  ['zgj83','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauA5dGu1f_F4zgSnZ-rqG4Y'],
  ['j6126','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatBox3IVf_F4zgSnZ-rqG4Y'],
  ['exx6r','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaolSsCuNf_F4zgSnZ-rqG4Y'],
  ['101vd','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap1qxxPHf_F4zgSnZ-rqG4Y'],
  ['39jrg','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaoeTdJonf_F4zgSnZ-rqG4Y'],
  ['m00yd','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatFqp_PHf_F4zgSnZ-rqG4Y'],
  ['l6g12','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatJoD1KVf_F4zgSnZ-rqG4Y'],
  ['1r05n','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap0kp9IIf_F4zgSnZ-rqG4Y'],
  ['m8ky0','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatGSlPNVf_F4zgSnZ-rqG4Y'],
  ['x9r6j','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauaTcSuIf_F4zgSnZ-rqG4Y'],
  ['55kwg','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlLMnf_F4zgSnZ-rqG4Y'],
];

const batchB = [
  ['60jxz','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqRqdFOGf_F4zgSnZ-rqG4Y'],
  ['8w6dm','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqYp6dnof_F4zgSnZ-rqG4Y'],
  ['55kym','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y'],
  ['qqwrm','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat0rEZrof_F4zgSnZ-rqG4Y'],
  ['n93ny','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatyTiRpmf_F4zgSnZ-rqG4Y'],
  ['mr3m0','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatEkiXlVf_F4zgSnZ-rqG4Y'],
  ['gxgm9','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatNSD3luf_F4zgSnZ-rqG4Y'],
  ['80d0m','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZqLjLof_F4zgSnZ-rqG4Y'],
  ['2zxnk','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEapgssBqof_F4zgSnZ-rqG4Y'],
  ['ll172','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGx8uVf_F4zgSnZ-rqG4Y'],
  ['xqz07','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauYrbjI1f_F4zgSnZ-rqG4Y'],
  ['50l0g','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplqLzInf_F4zgSnZ-rqG4Y'],
  ['0m05n','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap4Hp9IIf_F4zgSnZ-rqG4Y'],
  ['d0n78','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopq9MtOf_F4zgSnZ-rqG4Y'],
  ['9wz0j','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqUpbjKIf_F4zgSnZ-rqG4Y'],
  ['6exl0','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqQ_sNpVf_F4zgSnZ-rqG4Y'],
  ['ydq0v','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauU-zzINf_F4zgSnZ-rqG4Y'],
  ['lldl2','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGLtqVf_F4zgSnZ-rqG4Y'],
  ['zdm9j','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauA-TwuIf_F4zgSnZ-rqG4Y'],
  ['dx5jr','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopSSJmNf_F4zgSnZ-rqG4Y'],
  ['q9gjd','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat2TD5nHf_F4zgSnZ-rqG4Y'],
  ['nk9re','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatw9yJrnf_F4zgSnZ-rqG4Y'],
  ['e071e','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaolqCVLnf_F4zgSnZ-rqG4Y'],
  ['qd5kd','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat0-SDnHf_F4zgSnZ-rqG4Y'],
  ['9dmm0','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqU-T3lVf_F4zgSnZ-rqG4Y'],
  ['dkgd8','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaoo9D9lOf_F4zgSnZ-rqG4Y'],
  ['w0wm8','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauNqEXlOf_F4zgSnZ-rqG4Y'],
  ['lgkg7','YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatI5lLk1f_F4zgSnZ-rqG4Y'],
];

solveAndVerify('BatchA (25 tokens)', batchA);
solveAndVerify('BatchB (28 tokens)', batchB);
