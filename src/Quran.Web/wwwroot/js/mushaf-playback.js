(() => {
 const root=document.getElementById('warsh-pilot'),audio=document.getElementById('passage-player'),filter=document.getElementById('playback-wajh'),list=document.getElementById('playback-passages'),message=document.getElementById('playback-message');
 let passages=[],active=null,generation=0;
 function render(){list.replaceChildren();for(const p of passages.filter(p=>!filter.value||(p.wajhCatalogCode||'unqualified')===filter.value)){
  const button=document.createElement('button');button.type='button';button.className='btn btn-outline-success my-1';button.textContent=`${p.wajhName||'وجه غير محدد'} · ${p.segmentType===3?'إعادة':'مقطع'} · ${(p.startMs/1000).toFixed(2)}–${(p.endMs/1000).toFixed(2)} ث`;
  button.onclick=async()=>{active=p;audio.src=p.audioUrl;audio.currentTime=p.startMs/1000;try{await audio.play();message.textContent=p.status==='Published'?'تشغيل المقطع المنشور':'معاينة المقطع المحفوظ';}catch{message.textContent='تعذر التشغيل.';}};list.append(button);
 }}
 async function load(reference){const version=++generation;audio.pause();active=null;passages=[];list.replaceChildren();filter.replaceChildren(new Option('جميع الأوجه المتاحة',''));if(!reference)return;
  try{const r=await fetch('/api/annotations/playback?reference='+encodeURIComponent(reference)+'&preview='+!!root.dataset.previewPassage);if(!r.ok)throw Error();const data=await r.json();if(version!==generation)return;passages=data;const faces=new Map(passages.map(p=>[p.wajhCatalogCode||'unqualified',p.wajhName||'وجه غير محدد']));faces.forEach((name,code)=>filter.add(new Option(name,code)));render();message.textContent=passages.length?'اختر الوجه أو المقطع للاستماع.':'لا توجد مقاطع منشورة لهذه الآية بعد.';}catch{message.textContent='تعذر تحميل المقاطع.';}
 }
 function stopAtEnd(){if(active&&!audio.paused&&audio.currentTime*1000>=active.endMs)audio.pause();}
 audio.addEventListener('timeupdate',stopAtEnd);const timer=setInterval(stopAtEnd,40);window.addEventListener('pagehide',()=>clearInterval(timer));
 filter.onchange=render;root.addEventListener('ayah-selected',e=>load(e.detail));load(root.dataset.initialReference);
})();
