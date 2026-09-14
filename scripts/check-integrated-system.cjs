const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const assert=require('node:assert/strict');const {randomUUID}=require('node:crypto');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 const base=process.env.PILOT_URL||'http://127.0.0.1:5100';const api=page.request;
 let faceId=null,faceVersion=1,passageId=randomUUID(),passageVersion=null;
 const code='qa-'+randomUUID().slice(0,8);
 const face={code,nameArabic:'اختبار تقني مؤقت',routeCode:'azraq-shatibiyya',description:'اختبار الربط وليس وجهًا علميًا',sourceCitation:'اختبار آلي مؤقت',reviewStatus:'PendingReview',version:0};
 try{
  let r=await api.post(base+'/api/awjuh',{data:face});assert.equal(r.status(),200,await r.text());faceId=(await r.json()).id;
  const source=await (await api.get(base+'/data/baqarah-audio-review.json')).json();
  const input={edition:'MARW_newMushaf_20260913-full',source:{storageKey:'sources/002/original.mp3',sha256:source.sourceSha256},passages:[{id:passageId,startMs:120000,endMs:120400,reference:'2:1',segmentType:2,wajhCatalogCode:code,repeatOfId:null,notes:'اختبار مؤقت'}]};
  r=await api.post(base+'/api/annotations/import',{data:input});assert.equal(r.status(),200,await r.text());passageVersion=(await r.json())[0].serverVersion;
  // Edit only this synthetic record through the real annotator and verify automatic DB save.
  await page.goto(base+'/mushaf/audioreview');
  const seed=await (await api.get(base+'/api/annotations?sourceHash='+source.sourceSha256)).json();
  await page.evaluate(p=>{const root=document.getElementById('audio-review');localStorage.setItem(`audio-passages:${root.dataset.edition}:${root.dataset.sourceHash}`,JSON.stringify([p]));},seed.find(p=>p.id===passageId));
  await page.reload();await page.waitForFunction(()=>Number.isFinite(document.querySelector('audio').duration));
  await page.locator('#passage-list').getByText('تعديل',{exact:true}).click();
  await page.locator('#passage-note').fill('اختبار الحفظ الآلي');
  const saving=page.waitForResponse(r=>r.url().endsWith('/api/annotations/import')&&r.request().method()==='POST');
  await page.locator('#passage-form > button.btn-success').click();assert.equal((await saving).status(),200);
  let refreshed=await (await api.get(base+'/api/annotations?sourceHash='+source.sourceSha256)).json();
  passageVersion=refreshed.find(p=>p.id===passageId).serverVersion;assert.equal(refreshed.find(p=>p.id===passageId).notes,'اختبار الحفظ الآلي');
  assert.equal((await api.post(base+`/api/annotations/${passageId}/publish`,{data:{version:passageVersion,referenceChecked:true}})).status(),400);
  assert.equal((await api.delete(base+`/api/awjuh/${faceId}?version=1`)).status(),409);
  face.reviewStatus='Approved';face.version=1;r=await api.put(base+`/api/awjuh/${faceId}`,{data:face});assert.equal(r.status(),200);faceVersion=2;
  assert.equal((await api.put(base+`/api/awjuh/${faceId}`,{data:face})).status(),409);
  let stored=await (await api.get(base+'/api/annotations?sourceHash='+source.sourceSha256)).json();passageVersion=stored.find(p=>p.id===passageId).serverVersion;
  let published=await (await api.get(base+'/api/annotations/playback?reference=2:1')).json();assert.ok(!published.some(p=>p.id===passageId));
  await page.goto(base+`/mushaf/page?SurahNumber=2&PreviewPassage=${passageId}`);
  await page.waitForFunction(()=>document.getElementById('playback-passages').children.length>0);
  await page.locator('#playback-wajh').selectOption(code);assert.equal(await page.locator('#playback-passages button').count(),1);
  await page.locator('#playback-passages button').click();await page.waitForFunction(()=>{const a=document.getElementById('passage-player');return a.paused&&a.currentTime>=120.4;});
  r=await api.post(base+`/api/annotations/${passageId}/publish`,{data:{version:passageVersion,referenceChecked:true}});assert.equal(r.status(),200,await r.text());passageVersion=(await r.json()).serverVersion;
  published=await (await api.get(base+'/api/annotations/playback?reference=2:1')).json();assert.ok(published.some(p=>p.id===passageId));
  const unchanged=published.find(p=>p.id===passageId);
  r=await api.post(base+'/api/annotations/import',{data:{...input,passages:[unchanged]}});assert.equal(r.status(),200);assert.equal((await r.json())[0].status,'Published');
  face.version=2;face.description+=' modifié';r=await api.put(base+`/api/awjuh/${faceId}`,{data:face});assert.equal(r.status(),200);faceVersion=3;
  published=await (await api.get(base+'/api/annotations/playback?reference=2:1')).json();assert.ok(!published.some(p=>p.id===passageId));
  await page.goto(base+'/mushaf/awjuh');await page.locator(`[data-code="${code}"]`).getByText('تعديل',{exact:true}).click();assert.equal(await page.locator('#wajh-name').inputValue(),face.nameArabic);
  console.log('Integrated system passed: CRUD, stale version, referenced delete, import, draft preview, bounded playback, publication, edit invalidation.');
 }finally{
  if(faceId){const data=await(await api.get(base+'/api/awjuh/catalog')).json();faceVersion=data.entries.find(e=>e.id===faceId)?.version||faceVersion;}
  const source=await(await api.get(base+'/data/baqarah-audio-review.json')).json();const stored=await(await api.get(base+'/api/annotations?sourceHash='+source.sourceSha256)).json();const p=stored.find(p=>p.id===passageId);
  if(p)await api.delete(base+`/api/annotations/${passageId}?version=${p.serverVersion}`);
  if(faceId)await api.delete(base+`/api/awjuh/${faceId}?version=${faceVersion}`);
  await browser.close();
 }
})().catch(e=>{console.error(e);process.exit(1);});
