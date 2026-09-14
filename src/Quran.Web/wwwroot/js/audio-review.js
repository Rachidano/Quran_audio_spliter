(() => {
 'use strict';
 const root=document.getElementById('audio-review'); if(!root)return;
 const $=id=>document.getElementById(id), audio=$('review-audio');
 const key=`audio-passages:${root.dataset.edition}:${root.dataset.sourceHash}`;
 const allowedRefs=new Set([...$('passage-ayah').options].map(o=>o.value));
 const faceLabels={Unqualified:'لم يُحدَّد الوجه',SameObserved:'نفس الوجه بحسب الملاحظة — للمراجعة',DifferentObserved:'وجه آخر بحسب الملاحظة — للمراجعة'};
 let passages=[], selection=null, editing=null;
 let catalog=null;
 function restoreCatalog(code){
  const select=$('wajh-catalog');
  if(code&&![...select.options].some(o=>o.value===code))select.add(new Option(`${code} — مرجع غير متاح، للمراجعة`,code));
  select.value=code||'';
 }
 fetch('/api/awjuh/catalog').then(r=>{if(!r.ok)throw Error();return r.json();}).then(data=>{
  catalog=data;const selected=$('wajh-catalog').value;
  $('wajh-catalog').replaceChildren(new Option('غير محدد',''));
  data.entries.filter(e=>e.status!=='Archived'&&(!e.application||e.application.sourceSurah===2)).forEach(e=>$('wajh-catalog').add(new Option(`${data.routes.find(r=>r.code===e.routeCode).name} · ${e.nameArabic} — ${e.status==='Approved'?'معتمد':'للمراجعة'}`,e.code)));
  restoreCatalog(selected);$('wajh-catalog-status').textContent='مرجع جزئي؛ الاختيار اقتراح فقط ولا يثبت ملاءمة الوجه لهذه الآية.';
 }).catch(()=>$('wajh-catalog-status').textContent='المرجع غير متاح؛ يمكن متابعة ضبط الأوقات.');
 const message=text=>$('audio-message').textContent=text;
 function report(){return {schemaVersion:1,edition:root.dataset.edition,source:{storageKey:'sources/002/original.mp3',sha256:root.dataset.sourceHash},passages:passages.map((p,i)=>({...p,sequenceNumber:i+1}))};}
 async function systemRequest(url,options){const r=await fetch(url,options);if(!r.ok){let body={};try{body=await r.json();}catch{}throw Error(body.message||'تعذر الاتصال بالنظام');}return r.status===204?null:r.json();}
 async function storeInSystem(data){
  const stored=await systemRequest('/api/annotations/import',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});
  passages=stored;save();render();prepareNext();$('system-message').textContent='حُفظت المقاطع في النظام. المعاينة متاحة الآن.';
 }
 $('system-save').onclick=async()=>{try{await storeInSystem(report());}catch(e){$('system-message').textContent=e.message;}};
 $('system-load').onclick=async()=>{if(passages.length&&!confirm('استبدال المسودة المحلية بمقاطع النظام؟ صدّرها أولًا إذا أردت الاحتفاظ بها.'))return;try{passages=await systemRequest('/api/annotations?sourceHash='+root.dataset.sourceHash);save();render();prepareNext();$('system-message').textContent='تم الاسترجاع.';}catch(e){$('system-message').textContent=e.message;}};
 $('system-import').onchange=async event=>{const file=event.target.files[0];if(!file)return;try{const data=JSON.parse(await file.text());if(data.source?.sha256!==root.dataset.sourceHash||data.edition!==root.dataset.edition)throw Error('الملف لا يطابق مصدر هذه الورشة.');await storeInSystem(data);}catch(e){$('system-message').textContent=e.message;}event.target.value='';};
 try { const saved=JSON.parse(localStorage.getItem(key)||'[]');
  if(Array.isArray(saved)) passages=saved.filter(p=>p && typeof p.id==='string' && Number.isFinite(p.startMs) && p.startMs>=0 && Number.isFinite(p.endMs) && p.endMs>p.startMs && [1,2,3,4,99].includes(p.segmentType) && allowedRefs.has(p.reference||''));
 } catch {message('تعذر الحفظ المحلي: صدّر الملاحظات قبل المغادرة.');}
 function save(){try{localStorage.setItem(key,JSON.stringify(passages));}catch{message('تعذر الحفظ المحلي: صدّر الملاحظات قبل المغادرة.');}}
 function prepareNext(){
  if(!passages.length)return;
  const last=passages.reduce((a,b)=>b.endMs>=a.endMs?b:a);
  const endMs=last.endMs;
  const refs=[...allowedRefs].filter(Boolean), index=refs.indexOf(last.reference);
  const nextRef=last.segmentType===1 ? (index>=0?refs[index+1]||'':'') : last.reference||'';
  $('passage-start').value=(endMs/1000).toFixed(3);
  $('passage-end').value='';
  $('passage-ayah').value=nextRef;
  $('passage-type').value=last.segmentType===1&&nextRef?'1':'99';
  $('passage-note').value='';$('repeat-of').value='';
  $('wajh-observation').value='Unqualified';$('wajh-tariq').value='';$('wajh-description').value='';
  restoreCatalog(null);
  message(`المقطع التالي يبدأ عند ${(endMs/1000).toFixed(3)} ثانية${nextRef?`، الآية المقترحة ${nextRef}`:''}. حدد النهاية ثم احفظ. عند الإعادة، اختر الآية المعادة.`);
 }
 function bounds(){
  const start=Number($('passage-start').value),end=Number($('passage-end').value);
  if(!$('passage-start').value || !$('passage-end').value || !Number.isFinite(start)||!Number.isFinite(end)||start<0||end-start<.2||!Number.isFinite(audio.duration)||end>audio.duration){message('انتظر تحميل الصوت، ثم حدد مقطعًا صالحًا لا يقل عن 0.2 ثانية.');return null;}
  return {start,end};
 }
 async function play(start,end=null){selection=end===null?null:{start,end};audio.currentTime=start;try{await audio.play();}catch{message('تعذر التشغيل: تحقق من تحميل الصوت.');}}
 function render(){
  passages.sort((a,b)=>a.startMs-b.startMs||a.endMs-b.endMs||a.id.localeCompare(b.id));
  const list=$('passage-list');list.replaceChildren();
  $('repeat-of').replaceChildren(new Option('غير محدد / لا ينطبق',''));
  passages.forEach((p,i)=>{
   const li=document.createElement('li');li.className='my-2';
   const label=`${(p.startMs/1000).toFixed(3)}–${(p.endMs/1000).toFixed(3)} ثانية · ${p.reference||'آية غير محددة'} · ${p.segmentType===3?'إعادة مقطع':'مقطع للمراجعة'} · وجه غير معتمد`;
   const text=document.createElement('span');text.textContent=label+(p.notes?` · ${p.notes}`:'');li.append(text);
   const face=document.createElement('p');face.className='border-start border-success border-3 px-2';
   face.textContent=`أوجه الأداء: ${faceLabels[p.wajhObservation]||faceLabels.Unqualified}${p.wajhTariq?` · الطريق المقترح: ${p.wajhTariq}`:''}${p.wajhDescription?` · ${p.wajhDescription}`:''}`;li.append(face);
   if(p.wajhCatalogCode){const ref=document.createElement('p');ref.textContent=`مرجع مقترح: ${p.wajhCatalogCode} — غير معتمد`;li.append(ref);}
   if(p.serverVersion){
    const state=document.createElement('p');state.textContent=p.status==='Published'?'منشور في القارئ':'مسودة محفوظة في النظام';li.append(state);
    if(p.reference){const preview=document.createElement('a');preview.className='btn btn-outline-success';preview.href='/mushaf/page?SurahNumber=2&PreviewPassage='+encodeURIComponent(p.id);preview.target='_blank';preview.textContent='معاينة على المصحف';li.append(preview);}
    const publish=document.createElement('button');publish.type='button';publish.textContent='نشر المقطع';publish.className='btn btn-outline-success';publish.onclick=async()=>{try{if(!$('reference-checked').checked)throw Error('تحقق من ربط الآية ثم ضع علامة التأكيد.');const updated=await systemRequest(`/api/annotations/${p.id}/publish`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({version:p.serverVersion,referenceChecked:true})});passages=passages.map(x=>x.id===p.id?updated:x);save();render();}catch(e){$('system-message').textContent=e.message;}};li.append(publish);
    const remove=document.createElement('button');remove.type='button';remove.textContent='حذف من النظام';remove.className='btn btn-outline-danger';remove.onclick=async()=>{if(!confirm('حذف المقطع من النظام؟'))return;try{await systemRequest(`/api/annotations/${p.id}?version=${p.serverVersion}`,{method:'DELETE'});passages=passages.filter(x=>x.id!==p.id);save();render();}catch(e){$('system-message').textContent=e.message;}};li.append(remove);
   }
   for(const [name,action] of [
    ['استماع',()=>play(p.startMs/1000,p.endMs/1000)],
    ['تعديل',()=>{editing=p.id;restoreCatalog(p.wajhCatalogCode);$('wajh-observation').value=p.wajhObservation||'Unqualified';$('wajh-tariq').value=p.wajhTariq||'';$('wajh-description').value=p.wajhDescription||'';$('passage-start').value=p.startMs/1000;$('passage-end').value=p.endMs/1000;$('passage-ayah').value=p.reference||'';$('passage-type').value=p.segmentType;$('passage-note').value=p.notes||'';$('repeat-of').value=p.repeatOfId||'';message('وضع التعديل: زر الحفظ يحدّث هذا المقطع.');$('passage-form').scrollIntoView();}],
    ['حذف',()=>{passages=passages.filter(x=>x.id!==p.id);for(const x of passages)if(x.repeatOfId===p.id)x.repeatOfId=null;if(editing===p.id)editing=null;save();render();}]
   ]){const button=document.createElement('button');button.type='button';button.className='btn btn-sm btn-outline-secondary mx-1';button.textContent=name;button.addEventListener('click',action);li.append(button);}
   if(p.repeatOfId){const relation=document.createElement('small');relation.textContent=` إعادة للمقطع ${passages.findIndex(x=>x.id===p.repeatOfId)+1}`;li.append(relation);}
   list.append(li);$('repeat-of').add(new Option(`المقطع ${i+1} (${(p.startMs/1000).toFixed(1)} ثانية)`,p.id));
  });
 }
 $('mark-start').onclick=()=>$('passage-start').value=audio.currentTime.toFixed(3);
 $('mark-end').onclick=()=>$('passage-end').value=audio.currentTime.toFixed(3);
 $('preview-passage').onclick=()=>{const b=bounds();if(b)play(b.start,b.end);};
 $('audio-context').onclick=()=>play(0);
 $('audio-go').onclick=()=>{const n=Number($('audio-seek').value);if(Number.isFinite(n)&&n>=0&&n<audio.duration){selection=null;audio.currentTime=n;}else message('الموضع خارج مدة التسجيل.');};
 $('audio-rate').onchange=()=>audio.playbackRate=Number($('audio-rate').value);
 function tick(){
  $('audio-position').textContent=`الموضع: ${audio.currentTime.toFixed(3)} ثانية`;
  if(selection&&!audio.paused&&audio.currentTime>=selection.end){if($('loop-passage').checked)audio.currentTime=selection.start;else{audio.pause();selection=null;}}
 }
 audio.addEventListener('timeupdate',tick);const timer=setInterval(tick,40);window.addEventListener('pagehide',()=>clearInterval(timer));
 audio.addEventListener('error',()=>message('تعذرت قراءة الملف الصوتي الأصلي.'));
 $('passage-form').onsubmit=async event=>{
  event.preventDefault();const b=bounds();if(!b)return;
  const repeatOfId=$('repeat-of').value||null;
  if(repeatOfId===editing&&editing){message('لا يمكن ربط المقطع بنفسه.');return;}
  const target=passages.find(p=>p.id===repeatOfId);
  if(repeatOfId&&(!target||target.startMs>=Math.round(b.start*1000))){message('يجب أن تبدأ الإعادة بعد بداية المقطع المرتبط بها.');return;}
  if(editing&&passages.some(p=>p.repeatOfId===editing&&p.startMs<=Math.round(b.start*1000))){message('يجب أن يبدأ المقطع الأصلي قبل إعاداته.');return;}
  const entry={id:editing||crypto.randomUUID(),startMs:Math.round(b.start*1000),endMs:Math.round(b.end*1000),reference:$('passage-ayah').value||null,
   segmentType:Number($('passage-type').value),repeatOfId,wajhId:null,wajhStatus:'Unqualified',wajhObservation:$('wajh-observation').value,wajhTariq:$('wajh-tariq').value.trim(),wajhDescription:$('wajh-description').value.trim(),referenceStatus:'PendingReview',status:'Draft',notes:$('passage-note').value};
  if(repeatOfId&&entry.segmentType!==3){message('اختر « إعادة مقطع » لربطه بمقطع سابق.');return;}
  entry.serverVersion=passages.find(p=>p.id===editing)?.serverVersion;
  entry.wajhCatalogCode=$('wajh-catalog').value||null;
  entry.wajhCatalogRevision=entry.wajhCatalogCode?(catalog?.revision||passages.find(p=>p.id===editing)?.wajhCatalogRevision||null):null;
  const catalogEntry=catalog?.entries.find(e=>e.code===entry.wajhCatalogCode);
  if(catalogEntry)entry.wajhTariq=catalog.routes.find(r=>r.code===catalogEntry.routeCode).name;
  passages=passages.filter(p=>p.id!==entry.id);passages.push(entry);editing=null;save();render();prepareNext();
  const submit=$('passage-form').querySelector(':scope > button.btn-success');submit.disabled=true;
  try{await storeInSystem(report());}catch(e){$('system-message').textContent='المسودة محفوظة محليًا فقط: '+e.message;}finally{submit.disabled=false;}
 };
 $('export-passages').onclick=()=>{
  const report={schemaVersion:1,edition:root.dataset.edition,source:{storageKey:'sources/002/original.mp3',sha256:root.dataset.sourceHash},scope:'Draft listening annotations; ayah references and awjuh require review',passages:passages.map((p,i)=>({...p,sequenceNumber:i+1}))};
  const url=URL.createObjectURL(new Blob([JSON.stringify(report,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='baqarah-audio-passages-draft.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
 };
 fetch('/data/baqarah-audio-review.json').then(r=>{if(!r.ok)throw Error();return r.json();}).then(data=>{
  if(data.sourceSha256!==root.dataset.sourceHash)throw Error();
  const canvas=$('audio-waveform'),ctx=canvas.getContext('2d');ctx.fillStyle='#14634e';
  data.peaks.forEach((peak,i)=>ctx.fillRect(i*canvas.width/data.peaks.length,50-peak*48,Math.max(1,canvas.width/data.peaks.length),Math.max(1,peak*96)));
 }).catch(()=>{$('audio-waveform').hidden=true;message('مخطط الصوت غير متاح أو يخص ملفًا آخر. يبقى التشغيل متاحًا.');});
 render();prepareNext();
})();
