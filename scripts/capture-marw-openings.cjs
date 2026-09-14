// Local review evidence: original PNG crops, current overlay and source line indices.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs = require('node:fs');
const base = process.env.PILOT_URL || 'http://127.0.0.1:5098';
const dir = 'data/mushaf/editions/marw-newMushaf/20260913-full';
const output = process.env.OUTPUT_DIR || 'artifacts/marw-openings';
(async () => {
 const browser = await chromium.launch({ headless: true });
 try {
  const page = await browser.newPage();
  await page.goto(base + '/mushaf/review');
  const openings = [];
  for (let n = 1; n <= 604; n++) {
   const map = JSON.parse(fs.readFileSync(`${dir}/page-${String(n).padStart(3,'0')}.json`, 'utf8'));
   const first = map.rows.filter(r => r.ayah === 1);
   if (!first.length) continue;
   const response = await page.request.get(`${base}/api/mushaf/marw-pilot/pages/${n}`);
   const current = await response.json(); await response.dispose();
   for (const row of first) openings.push({ n, row, boxes: map.boxes, current: current.page.regions.find(r => r.reference === `${row.surah}:1`), imageUrl: current.imageUrl });
  }
  fs.mkdirSync(output, { recursive: true });
  fs.writeFileSync(`${output}/index.json`, JSON.stringify(openings, null, 2));
  for (let offset = 0; offset < openings.length; offset += 4) {
   const url = await page.evaluate(async items => {
    const canvas = document.createElement('canvas'); canvas.width = 1700; canvas.height = 1500;
    const ctx = canvas.getContext('2d'); ctx.fillStyle = 'white'; ctx.fillRect(0,0,1700,1500);
    for (let k = 0; k < items.length; k++) {
     const o = items[k], c = o.boxes.cropBox, t = o.boxes.textBox;
     const img = new Image(); img.src = o.imageUrl; await img.decode();
     const W = img.naturalWidth, H = img.naturalHeight;
     const tx = (c.left+c.width*t.left/100)*W/100, ty = (c.top+c.height*t.top/100)*H/100;
     const tw = c.width*t.width*W/10000, th = c.height*t.height*H/10000;
     const lines = o.n <= 2 ? 7 : 15, lh = th/lines;
     const top = Math.max(0, Math.min(...o.row.rects.map(r=>r.top))*th/100 + ty - lh*.45);
     const bottom = Math.min(H, Math.max(...o.row.rects.map(r=>r.top+r.height))*th/100 + ty + lh*.5);
     const scale = Math.min(780/tw, 665/(bottom-top));
     const x = (k%2)*850 + 55, y = Math.floor(k/2)*750+65;
     ctx.fillStyle = '#111'; ctx.font = '20px sans-serif';
     ctx.fillText(`Surah ${o.row.surah}:1 | page ${o.n} | line indices 0..${lines-1}`, x-40, y-30);
     ctx.save(); ctx.beginPath(); ctx.rect(x,y,tw*scale,(bottom-top)*scale); ctx.clip();
     ctx.translate(x-tx*scale,y-top*scale); ctx.scale(scale,scale);
     ctx.drawImage(img,0,0); ctx.fillStyle='rgba(230,170,30,.25)'; ctx.fill(new Path2D(o.current.pathData)); ctx.restore();
     ctx.font='17px sans-serif'; ctx.fillStyle='#b00020';
     for(let line=0;line<lines;line++) {
      const ly=y+(ty+(line+.5)*lh-top)*scale;
      if(ly>y && ly<y+(bottom-top)*scale) ctx.fillText(String(line), x-30,ly);
     }
    }
    return canvas.toDataURL('image/png').split(',')[1];
   }, openings.slice(offset, offset+4));
   fs.writeFileSync(`${output}/sheet-${String(offset/4+1).padStart(2,'0')}.png`, Buffer.from(url,'base64'));
  }
  console.log(`Captured ${openings.length} openings in 29 sheets.`);
 } finally { await browser.close(); }
})().catch(e=>{ console.error(e); process.exitCode=1; });
