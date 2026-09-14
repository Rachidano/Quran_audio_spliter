const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const base = process.env.PILOT_URL || 'http://127.0.0.1:5098';
const review = JSON.parse(fs.readFileSync('src/Quran.Infrastructure/Data/marw-opening-review.json','utf8'));
const near = (a,b) => Math.abs(a-b) < 1e-6;
(async () => {
 const browser = await chromium.launch({headless:true});
 try {
  const page = await browser.newPage();
  await page.goto(base + '/mushaf/review');
  let unchanged=0, openings=0;
  const probes=[];
  for(let n=1;n<=604;n++) {
   const source=JSON.parse(fs.readFileSync(`data/mushaf/editions/marw-newMushaf/20260913-full/page-${String(n).padStart(3,'0')}.json`,'utf8'));
   const response=await page.request.get(`${base}/api/mushaf/marw-pilot/pages/${n}`);
   assert.equal(response.status(),200);
   const {page:data}=await response.json(); await response.dispose();
   const c=source.boxes.cropBox,t=source.boxes.textBox;
   const tx=(c.left+c.width*t.left/100)*data.width/100,ty=(c.top+c.height*t.top/100)*data.height/100;
   const tw=c.width*t.width*data.width/10000,th=c.height*t.height*data.height/10000;
   for(const row of source.rows) {
    const region=data.regions.find(r=>r.reference===`${row.surah}:${row.ayah}`);
    assert.ok(region);
    const values=region.pathData.match(/[-+]?(?:\d*\.)?\d+(?:[eE][-+]?\d+)?/g).map(Number);
    if(row.ayah!==1) {
     const expected=row.rects.flatMap(r=>{const x=tx+tw*r.left/100,y=ty+th*r.top/100,w=tw*r.width/100,h=th*r.height/100;return [x,y,x+w,y,x+w,y+h,x,y+h];});
     assert.equal(values.length,expected.length);
     assert.ok(values.every((v,i)=>near(v,expected[i])),`Non-opening changed: ${region.reference}`);
     unchanged++;
    } else {
     const entry=review.Entries[row.surah-1];
     assert.equal(entry.Page,n);
     const y=ty+th*entry.FirstTextLine/entry.LineCount;
     const ys=values.filter((v,i)=>i%2===1);
     assert.ok(near(Math.min(...ys),y),`Start line: ${region.reference}`);
     const lastBottom=Math.max(...row.rects.map(r=>r.top+r.height));
     assert.ok(near(Math.max(...ys),ty+th*lastBottom/100),`End clipped: ${region.reference}`);
     const marker=[tx+tw*row.left/100,ty+th*row.top/100];
     const first=[tx+tw*.98,y+th/entry.LineCount*.5];
     const header=[tx+tw*.5,y-th/entry.LineCount*.5];
     probes.push({reference:region.reference,path:region.pathData,marker,first,header});
     openings++;
    }
   }
  }
  const results=await page.evaluate(probes=>{
   const ctx=document.createElement('canvas').getContext('2d');
   return probes.map(p=>{const path=new Path2D(p.path);return {reference:p.reference,marker:ctx.isPointInPath(path,...p.marker),first:ctx.isPointInPath(path,...p.first),header:ctx.isPointInPath(path,...p.header)};});
  },probes);
  for(const p of results) { assert.ok(p.marker,`End marker not selectable: ${p.reference}`);assert.ok(p.first,`First text line missing: ${p.reference}`);assert.equal(p.header,false,`Header selectable: ${p.reference}`); }
  assert.equal(openings,114);assert.equal(unchanged,6100);
  fs.writeFileSync('artifacts/marw-opening-checks.json',JSON.stringify({openings,unchanged,probes:results},null,2));
  console.log('OK: 114 first text lines and end markers preserved; headers excluded; 6100 other ayat unchanged.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
