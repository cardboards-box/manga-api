const fs=require("fs");
const b=fs.readFileSync("current-secure-bundle.js","utf8");
const re=/"([A-Za-z0-9+\/]{60,}={0,2})"/g;
let m,n=0;
while((m=re.exec(b))!==null){
  if(n<8)console.log(n,m[1].substring(0,80));
  n++;
}
console.log("total:",n);
