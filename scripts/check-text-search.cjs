const {chromium} = require('playwright');
const assert = require('node:assert/strict');
const base = process.env.QURAN_BASE_URL || 'http://127.0.0.1:5098';
(async()=>{
 const browser = await chromium.launch({channel:'msedge',headless:true});
 try {
  const page = await browser.newPage();
  for (const q of ['ذلك الكتاب','ذَٰلِكَ الْكِتَابُ','الحمد لله','العالمين','يؤمنون']) {
   const response = await page.request.get(base+'/api/search?q='+encodeURIComponent(q));
   assert.equal(response.status(),200);
   const data = await response.json();assert.ok(data.length>0,q);
   assert.ok(data.every(r=>r.isProvisional&&r.textWarsh.length>0&&r.matchedText===''&&r.pageNumber>0));
   console.log(q+': '+data.length+' references');
  }
  const phrase = await (await page.request.get(base+'/api/search?q='+encodeURIComponent('الله لا'))).json();
  assert.ok(phrase.length>0);
  assert.ok(!phrase.some(r=>r.surahNumber===2 && [59,160,186,194].includes(r.ayahNumber)));
  for (const ayah of [25,189,253]) assert.ok(phrase.some(r=>r.surahNumber===2 && r.ayahNumber===ayah));
  console.log('PASS الله لا excludes false positives and retains consecutive matches');
  const all = await (await page.request.get(base+'/api/search?q='+encodeURIComponent('الله'))).json();
  assert.ok(all.length>100, 'Results must extend beyond the old 20/100 caps');
  await page.goto(base+'/?q='+encodeURIComponent('الله'));
  assert.equal(await page.locator('.list-group > a').count(),20);
  assert.ok((await page.locator('h4').first().textContent()).includes(String(all.length)));
  const first = await page.locator('.list-group > a').first().getAttribute('href');
  await page.locator('.search-pagination').getByRole('link',{name:'التالية',exact:true}).click();
  assert.equal(new URL(page.url()).searchParams.get('q'),'الله');
  assert.notEqual(await page.locator('.list-group > a').first().getAttribute('href'),first);
  await page.locator('.search-pagination').getByRole('link',{name:'الأخيرة',exact:true}).click();
  const last = all.at(-1);
  assert.equal(await page.locator('.list-group > a').last().getAttribute('href'),`/quran/${last.surahNumber}/${last.ayahNumber}`);
  await page.goto(base+'/?q='+encodeURIComponent('فِـي'));
  assert.ok((await page.getByRole('alert').textContent()).includes('ثلاثة أحرف'));
  assert.equal(await page.locator('.list-group > a').count(),0);
  console.log('PASS all results, next/last pages, three actual letters minimum');
  const ref = await (await page.request.get(base+'/api/search?q='+encodeURIComponent('٢:٤'))).json();
  assert.equal(ref[0].ayahNumber,4);assert.equal(ref[0].isProvisional,false);
  await page.goto(base+'/?q='+encodeURIComponent('ذلك الكتاب'));
  assert.ok(await page.locator('.quran-text').count()>0);
  assert.ok((await page.locator('.quran-text').first().textContent()).includes('ذَٰلِكَ'));
  assert.ok((await page.locator('[role=status]').textContent()).includes('تجريبي'));
  await page.locator('.list-group a[href="/quran/2/1"]').click();
  await page.waitForURL(u=>u.pathname==='/mushaf/warsh/page/2'&&u.searchParams.get('Reference')==='2:1');
  await page.waitForFunction(()=>document.getElementById('pilot-image')?.naturalWidth>0);
  console.log('PASS diacritics, phrase search, Arabic digits, provisional verse previews, MARW image navigation');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
