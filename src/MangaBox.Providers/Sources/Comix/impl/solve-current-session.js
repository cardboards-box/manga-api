'use strict';
// Solve for CURRENT session masks using 90 captured tokens (session verified = batchB family)

const allCaptured = [
  ['5r7m', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEapkkCXnvO5CB39rUUwyzOw'],
  ['55kym', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlPPof_F4zgSnZ-rqG4Y'],
  ['936j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqVl6ZnvO5CB39rUUwyzOw'],
  ['qqwrm', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat0rEZrof_F4zgSnZ-rqG4Y'],
  ['n93ny', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatyTiRpmf_F4zgSnZ-rqG4Y'],
  ['mr3m0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatEkiXlVf_F4zgSnZ-rqG4Y'],
  ['gxgm9', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatNSD3luf_F4zgSnZ-rqG4Y'],
  ['8v88', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZqLnLvO5CB39rUUwyzOw'],
  ['80d0m', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZqLjLof_F4zgSnZ-rqG4Y'],
  ['nr83', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatyTiRovO5CB39rUUwyzOw'],
  ['2zxnk', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEapgssBqof_F4zgSnZ-rqG4Y'],
  ['ll172', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGx8uVf_F4zgSnZ-rqG4Y'],
  ['xqz07', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauYrbjI1f_F4zgSnZ-rqG4Y'],
  ['793e', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqUpbzLvO5CB39rUUwyzOw'],
  ['d0n78', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopq9MtOf_F4zgSnZ-rqG4Y'],
  ['50l0g', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplqLzInf_F4zgSnZ-rqG4Y'],
  ['e3jg', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaoksLCnvO5CB39rUUwyzOw'],
  ['0m05n', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap4Hp9IIf_F4zgSnZ-rqG4Y'],
  ['l7re', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGLCnvO5CB39rUUwyzOw'],
  ['qk31', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat0rE7nvO5CB39rUUwyzOw'],
  ['n8we', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatyTiJnvO5CB39rUUwyzOw'],
  ['6exl0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqQ_sNpVf_F4zgSnZ-rqG4Y'],
  ['9wz0j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqUpbjKIf_F4zgSnZ-rqG4Y'],
  ['ydq0v', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauU-zzINf_F4zgSnZ-rqG4Y'],
  ['lldl2', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGLtqVf_F4zgSnZ-rqG4Y'],
  ['zdm9j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauA-TwuIf_F4zgSnZ-rqG4Y'],
  ['dx5jr', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopSSJmNf_F4zgSnZ-rqG4Y'],
  ['13ml', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap1qx7nvO5CB39rUUwyzOw'],
  ['q9gjd', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat2TD5nHf_F4zgSnZ-rqG4Y'],
  ['nk9re', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatw9yJrnf_F4zgSnZ-rqG4Y'],
  ['dkgd8', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaoo9D9lOf_F4zgSnZ-rqG4Y'],
  ['e071e', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaolqCVLnf_F4zgSnZ-rqG4Y'],
  ['w0wm8', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauNqEXlOf_F4zgSnZ-rqG4Y'],
  ['lw2j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatIGLSnvO5CB39rUUwyzOw'],
  ['qd5kd', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat0-SDnHf_F4zgSnZ-rqG4Y'],
  ['9dmm0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqU-T3lVf_F4zgSnZ-rqG4Y'],
  ['keyz', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatOTiZnvO5CB39rUUwyzOw'],
  ['m12d', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatEkiCnvO5CB39rUUwyzOw'],
  ['lgkg7', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatI5lLk1f_F4zgSnZ-rqG4Y'],
  ['70w00', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqNqETJVf_F4zgSnZ-rqG4Y'],
  ['9nwr', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqUrb7nvO5CB39rUUwyzOw'],
  ['3rq32', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaockz5KVf_F4zgSnZ-rqG4Y'],
  ['166d', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap1qxJnvO5CB39rUUwyzOw'],
  ['vw9zy', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauQpyJNmf_F4zgSnZ-rqG4Y'],
  ['3v80', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaoegCnvO5CB39rUUwyzOw'],
  ['rm7l', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatEkiCmvO5CB39rUUwyzOw'],
  ['w0ren', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauNqcXgIf_F4zgSnZ-rqG4Y'],
  ['3rdr3', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaockLpq1f_F4zgSnZ-rqG4Y'],
  ['87dld', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZpLtrHf_F4zgSnZ-rqG4Y'],
  ['eqy5m', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaokr0NLof_F4zgSnZ-rqG4Y'],
  ['d19rg', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaopryJonf_F4zgSnZ-rqG4Y'],
  ['k9317', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEat-TiVI1f_F4zgSnZ-rqG4Y'],
  ['vw2lq', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauQpadptf_F4zgSnZ-rqG4Y'],
  ['7ldne', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqMGLhrnf_F4zgSnZ-rqG4Y'],
  ['yegj1', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauU_D5l1f_F4zgSnZ-rqG4Y'],
  ['zgj83', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauA5dGu1f_F4zgSnZ-rqG4Y'],
  ['j6126', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatBox3IVf_F4zgSnZ-rqG4Y'],
  ['w9k2g', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauOTlHInf_F4zgSnZ-rqG4Y'],
  ['exx6r', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaolSsCuNf_F4zgSnZ-rqG4Y'],
  ['l96ee', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatJqCRmnf_F4zgSnZ-rqG4Y'],
  ['0r65', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap4DdJnvO5CB39rUUwyzOw'],
  ['39jrg', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaoeTdJonf_F4zgSnZ-rqG4Y'],
  ['m00yd', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatFqp_PHf_F4zgSnZ-rqG4Y'],
  ['l6g12', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatJoD1KVf_F4zgSnZ-rqG4Y'],
  ['1r05n', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap0kp9IIf_F4zgSnZ-rqG4Y'],
  ['j2kv', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatI5lhnvO5CB39rUUwyzOw'],
  ['5yrz', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaplvlJnvO5CB39rUUwyzOw'],
  ['m8ky0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatGSlPNVf_F4zgSnZ-rqG4Y'],
  ['ol1z', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaol5l7nvO5CB39rUUwyzOw'],
  ['x9r6j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauaTcSuIf_F4zgSnZ-rqG4Y'],
  ['xly9j', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauYGsSuIf_F4zgSnZ-rqG4Y'],
  ['x8gj', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauaTiJnvO5CB39rUUwyzOw'],
  ['xlyyj', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauYG0POIf_F4zgSnZ-rqG4Y'],
  ['ymmr', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauUHT5rvO5CB39rUUwyzOw'],
  ['nnx3e', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatwgsJLnf_F4zgSnZ-rqG4Y'],
  ['6elqg', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqQ_L_onf_F4zgSnZ-rqG4Y'],
  ['e07xe', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaolqCVPnf_F4zgSnZ-rqG4Y'],
  ['grrz7', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatMkcZM1f_F4zgSnZ-rqG4Y'],
  ['9wj0', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqUpdDLvO5CB39rUUwyzOw'],
  ['zxry6', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauBScfMVf_F4zgSnZ-rqG4Y'],
  ['8d9ye', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqY-yPPnf_F4zgSnZ-rqG4Y'],
  ['y5nn', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauVv9BrvO5CB39rUUwyzOw'],
  ['25j2', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaphvdHLvO5CB39rUUwyzOw'],
  ['0klwl', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap49L7PIf_F4zgSnZ-rqG4Y'],
  ['81glm', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqZrD9rof_F4zgSnZ-rqG4Y'],
  ['zgk96', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEauA5lAsVf_F4zgSnZ-rqG4Y'],
  ['rg5k8', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEatg5SDlOf_F4zgSnZ-rqG4Y'],
  ['2015n', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaphqx9IIf_F4zgSnZ-rqG4Y'],
  ['1013n', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEap1qx5IIf_F4zgSnZ-rqG4Y'],
  ['605ng', 'YchAkKIHsEZwpi6s3QD9H8QKGSBjiB3l0g2WM4T81UgQ3lUQMcWv5mQGeOkg-aeEaqRqSBonf_F4zgSnZ-rqG4Y'],
];

function fromB64url(s) {
  return [...Buffer.from(s.replace(/-/g, '+').replace(/_/g, '/'), 'base64')];
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
  const pc = []; let row = 0;
  for (let col = 0; col < n && row < m; col++) {
	let pr = -1;
	for (let r = row; r < m; r++) if (mat[r][col] === 1) { pr = r; break; }
	if (pr < 0) continue;
	[mat[row], mat[pr]] = [mat[pr], mat[row]];
	pc.push({ row, col });
	for (let r = 0; r < m; r++) {
	  if (r !== row && mat[r][col] === 1)
		for (let c = col; c <= n; c++) mat[r][c] ^= mat[row][c];
	}
	row++;
  }
  for (let r = row; r < m; r++) if (mat[r][n] === 1) return null;
  const x = new Array(n).fill(0);
  for (const { row: r, col: c } of pc) x[c] = mat[r][n];
  return x;
}

function solveGroup(name, oracles, charCount, varBits) {
  const allB = oracles.map(([, tok]) => fromB64url(tok));
  const pfx = allB[0].slice(0, 49);
  const sfxStart = 49 + varBits / 8;
  const sfx = allB[0].slice(sfxStart);

  const A = oracles.map(([id]) => buildFB(id, charCount));
  const masks = [];
  let inc = 0;
  for (let ob = 0; ob < varBits; ob++) {
	const byteIdx = 49 + (ob >> 3), bitIdx = ob & 7;
	const b_col = allB.map(bytes => (bytes[byteIdx] >> bitIdx) & 1);
	const sol = solvGF2(A, b_col);
	if (!sol) { inc++; masks.push(0n); }
	else masks.push(maskBitsToNum(sol));
  }
  console.log(name + ': inconsistent bits = ' + inc);

  const pfxBuf = Buffer.from(pfx), sfxBuf = Buffer.from(sfx);
  let pass = 0, fail = 0;
  for (const [id, expected] of oracles) {
	const bytes = Buffer.alloc(pfxBuf.length + varBits / 8 + sfxBuf.length);
	pfxBuf.copy(bytes);
	const fb = buildFB(id, charCount);
	for (let ob = 0; ob < varBits; ob++) {
	  let p = 0;
	  for (let b = 0; b < fb.length; b++) if (fb[b] && ((masks[ob] >> BigInt(b)) & 1n)) p ^= 1;
	  if (p) bytes[49 + (ob >> 3)] |= (1 << (ob & 7));
	}
	sfxBuf.copy(bytes, 49 + varBits / 8);
	const got = bytes.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	if (got === expected) pass++;
	else { fail++; console.log('  FAIL ' + id + '\n    exp: ' + expected + '\n    got: ' + got); }
  }
  console.log(name + ': ' + pass + '/' + oracles.length + ' PASS, ' + fail + ' FAIL');

  if (pass === oracles.length) {
	console.log('\n=== ' + name + ' suffix ===');
	console.log(JSON.stringify(sfx));
	console.log('\n=== ' + name + ' masks (' + varBits + ' elements) ===');
	for (let i = 0; i < varBits; i += 8)
	  console.log('\t\t' + masks.slice(i, i + 8).map(n => n.toString()).join(', ') + ',');
  }
  return pass === oracles.length ? { masks, pfx, sfx } : null;
}

const shortOracles = allCaptured.filter(([id]) => id.length <= 4);
const longOracles = allCaptured.filter(([id]) => id.length === 5);
console.log('Short IDs: ' + shortOracles.length + ', Long IDs: ' + longOracles.length);

const shortResult = solveGroup('Short (4-char, 32-bit)', shortOracles, 4, 32);
const longResult = solveGroup('Long (5-char, 40-bit)', longOracles, 5, 40);
