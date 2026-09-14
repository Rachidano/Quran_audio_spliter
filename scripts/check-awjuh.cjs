const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const assert=require('node:assert/strict');
const fs=require('node:fs');
(async()=>{
 const browser=await chromium.launch({headless:true});
 try {
  const page=await browser.newPage();
  await page.route('**/api/annotations/import', route => { const data=route.request().postDataJSON();return route.fulfill({json:data.passages.map(p=>({...p,serverVersion:1,status:'Draft'}))}); });
  await page.goto('http://127.0.0.1:5098/mushaf/awjuh');
  await page.waitForFunction(()=>document.querySelectorAll('#catalog-entries article').length===14);
  await page.locator('#catalog-route').selectOption('asbahani-tayyiba');
  assert.equal(await page.locator('article').count(),10);
  assert.equal(await page.locator('[data-constraint]').count(),4);
  await page.locator('#catalog-route').selectOption('azraq-shatibiyya');
  assert.equal(await page.locator('article').count(),4);
  assert.equal(await page.locator('[data-constraint]').count(),0);
  await page.goto('http://127.0.0.1:5098/mushaf/audioreview');
  await page.waitForFunction(()=>document.querySelector('#wajh-catalog').options.length===5&&Number.isFinite(document.querySelector('audio').duration));
  await page.locator('#wajh-catalog').selectOption('az-sh-badal2-lin4');
  await page.locator('#passage-form > button.btn-success').click();
  await page.locator('#passage-list').getByText('تعديل',{exact:true}).click();
  assert.equal(await page.locator('#wajh-catalog').inputValue(),'az-sh-badal2-lin4');
  await page.locator('#passage-form > button.btn-success').click();
  await page.reload();
  const download=page.waitForEvent('download');await page.locator('#export-passages').click();
  const data=JSON.parse(fs.readFileSync(await(await download).path(),'utf8'));
  assert.equal(data.passages[0].wajhCatalogCode,'az-sh-badal2-lin4');
  assert.equal(data.passages[0].wajhCatalogRevision,'database');
  assert.equal(data.passages[0].wajhId,null);assert.equal(data.passages[0].wajhStatus,'Unqualified');
  console.log('Awjuh: route filtering, selection, edit, persistence and unqualified export passed.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
