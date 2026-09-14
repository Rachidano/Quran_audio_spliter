(() => {
 'use strict';
 const audio=document.getElementById('demo-audio'), status=document.getElementById('demo-status');
 const pages=[...document.querySelectorAll('.demo-page')], paths=[...document.querySelectorAll('.pilot-regions path')];
 let passages=[], finish=0, lastLabel='';
 function update(){
  const ms=audio.currentTime*1000;
  if(finish&&ms>=finish&&!audio.paused)audio.pause();
  const active=passages.find(p=>p.startMs<=ms&&ms<p.endMs);
  paths.forEach(path=>path.classList.toggle('selected',!!active&&path.dataset.reference===active.reference));
  if(active){const visible=paths.find(p=>p.dataset.reference===active.reference)?.closest('.demo-page');if(visible)pages.forEach(p=>p.hidden=p!==visible);}
  const label=active?`المقطع ${active.sequenceNumber} — الآية المقترحة ${active.reference}${active.wajhObservation==='DifferentObserved'?' — ملاحظة المستخدم: وجه آخر محتمل، غير معتمد':''}`:ms>=finish&&finish?'نهاية العينة':'فترة غير معلَّمة — لا تُنسب إلى آية';
  if(label!==lastLabel){status.textContent=label;lastLabel=label;}
 }
 async function play(p){audio.currentTime=p.startMs/1000;update();try{await audio.play();}catch{status.textContent='تعذر تشغيل الصوت. اضغط تشغيل في المشغل.';}}
 audio.addEventListener('timeupdate',update);audio.addEventListener('seeked',update);
 const timer=setInterval(update,40);window.addEventListener('pagehide',()=>clearInterval(timer));
 pages[0].hidden=false;
 Promise.all([fetch('/data/baqarah-demo-draft.json').then(r=>{if(!r.ok)throw Error();return r.json();}),fetch('/data/baqarah-audio-review.json').then(r=>{if(!r.ok)throw Error();return r.json();})]).then(([data,source])=>{
  if(data.edition!=='MARW_newMushaf_20260913-full'||data.source.sha256!==source.sourceSha256)throw Error();
  passages=data.passages;finish=Math.max(...passages.map(p=>p.endMs));
  for(const p of passages){const button=document.createElement('button');button.className='btn btn-outline-success';button.textContent=`${p.reference} · ${(p.startMs/1000).toFixed(3)}–${(p.endMs/1000).toFixed(3)} ث`;button.onclick=()=>play(p);document.getElementById('demo-passages').append(button);}
  paths.forEach(path=>path.addEventListener('click',()=>{const p=passages.find(p=>p.reference===path.dataset.reference);if(p)play(p);}));
  const start=document.getElementById('demo-play');start.disabled=false;start.onclick=()=>play(passages[0]);update();
 }).catch(()=>{status.textContent='تعذر تحميل الملاحظات أو لا تطابق مصدر التجربة.';});
})();
