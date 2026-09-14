const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});
 try {
  const page=await browser.newPage();
  assert.equal((await page.goto('http://127.0.0.1:5098/mushaf/audiodemo')).status(),200);
  await page.waitForFunction(()=>!document.getElementById('demo-play').disabled&&Number.isFinite(document.querySelector('audio').duration));
  assert.equal(await page.locator('#demo-passages button').count(),5);
  async function seek(seconds){await page.locator('audio').evaluate((a,t)=>{a.pause();a.currentTime=t;},seconds);}
  for(const [time,ref] of [[15,'2:1'],[24,'2:2'],[35,'2:3'],[50,'2:4'],[85,'2:5']]){
   await seek(time);
   await page.waitForFunction(ref=>[...document.querySelectorAll('path.selected')].some(p=>p.dataset.reference===ref),ref);
   assert.ok(await page.locator('path.selected').first().isVisible());
  }
  assert.match(await page.locator('#demo-status').textContent(),/غير معتمد/);
  await seek(70);await page.waitForFunction(()=>!document.querySelector('path.selected'));
  assert.match(await page.locator('#demo-status').textContent(),/غير معلَّمة/);
  await page.locator('#demo-play').click();
  await page.waitForFunction(()=>!document.querySelector('audio').paused);
  await seek(50);
  await page.screenshot({path:'artifacts/audio-demo.png',fullPage:true});
  await page.setViewportSize({width:390,height:844});
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  console.log('Demo passed: five references, visible pages, unannotated gap, draft wajh label, playback, mobile.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
