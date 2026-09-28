(() => {
 const $=id=>document.getElementById(id);
 const states={PendingReview:'قيد المراجعة',Approved:'معتمد',Archived:'مؤرشف'};
 if ($('wajh-new') && $('wajh-form')) {
  $('wajh-new').onclick=()=>{$('wajh-form').reset();$('wajh-edit-id').value='';$('wajh-edit-version').value='0';$('wajh-code').readOnly=false;};
  $('wajh-form').onsubmit=async event=>{
   event.preventDefault();const id=$('wajh-edit-id').value;
   const payload={code:$('wajh-code').value,nameArabic:$('wajh-name').value,routeCode:$('wajh-route').value,description:$('wajh-scope').value,sourceCitation:$('wajh-source').value,reviewStatus:$('wajh-state').value,version:Number($('wajh-edit-version').value)};
   try{const r=await fetch('/api/awjuh'+(id?'/'+id:''),{method:id?'PUT':'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)});if(!r.ok)throw Error((await r.json()).message||'تعذر الحفظ');location.reload();}catch(e){$('wajh-feedback').textContent=e.message;}
  };
 }
 const route=document.getElementById('catalog-route'),search=document.getElementById('catalog-search'),list=document.getElementById('catalog-entries'),status=document.getElementById('catalog-status');
 fetch('/api/awjuh/catalog').then(r=>{if(!r.ok)throw Error();return r.json();}).then(data=>{
  data.routes.forEach(r=>route.add(new Option(r.name+(r.coverage==='NotDocumented'?' — لم يوثّق بعد':''),r.code)));
  function render(){
   list.replaceChildren();
   const constraints=document.getElementById('catalog-constraints');constraints.replaceChildren();
   const selectedConstraints=(data.constraints||[]).filter(e=>(!route.value||e.routeCode===route.value)&&JSON.stringify(e).includes(search.value.trim()));
   selectedConstraints.forEach(e=>{
    const card=document.createElement('section');card.className='border rounded p-3 my-3';card.dataset.constraint=e.code;
    for(const [tag,text] of [['h3',e.nameArabic],['p',e.contextArabic],['p',e.explanationArabic],['p','قيد مقترح — لم يعتمد ولم يُفعّل آليًا']]){const el=document.createElement(tag);el.textContent=String(text ?? '').replace(/MARW/g, 'مصدرنا الرسمي');card.append(el);}
    e.sourceIds.forEach(id=>{const s=data.sources.find(s=>s.id===id),a=document.createElement('a');a.href=s.url;a.textContent=s.locator;a.target='_blank';a.rel='noopener';card.append(a);});constraints.append(card);
   });
   if(!selectedConstraints.length)constraints.textContent='لا توجد قيود مدخلة لهذا الاختيار بعد.';
   const entries=data.entries.filter(e=>(!route.value||e.routeCode===route.value)&&JSON.stringify(e).includes(search.value.trim()));
   status.textContent=entries.length?`${entries.length} مداخل في قاعدة النظام`:'لا توجد مداخل لهذا الاختيار.';
   entries.forEach(e=>{
    const card=document.createElement('article');card.className='border rounded p-3 my-3';card.dataset.code=e.code;
    if ($('wajh-form-panel')) {
     const edit=document.createElement('button');edit.type='button';edit.className='btn btn-outline-success';edit.textContent='تعديل';edit.onclick=()=>{
      $('wajh-form-panel').open=true;$('wajh-edit-id').value=e.id;$('wajh-edit-version').value=e.version;$('wajh-code').value=e.code;$('wajh-code').readOnly=true;$('wajh-name').value=e.nameArabic;$('wajh-route').value=e.routeCode;$('wajh-scope').value=e.scope||'';$('wajh-source').value=e.sourceCitation||'';$('wajh-state').value=e.status;$('wajh-form-panel').scrollIntoView();
     };card.append(edit);
     const remove=document.createElement('button');remove.type='button';remove.className='btn btn-outline-danger mx-2';remove.textContent='حذف';remove.onclick=async()=>{if(!confirm('حذف هذا الوجه غير المستخدم من النظام؟'))return;try{const r=await fetch(`/api/awjuh/${e.id}?version=${e.version}`,{method:'DELETE'});if(!r.ok)throw Error((await r.json()).message||'تعذر الحذف');location.reload();}catch(err){status.textContent=err.message;}};card.append(remove);
    }
    for(const [tag,text] of [['h2',e.nameArabic],['p',data.routes.find(r=>r.code===e.routeCode).name],['p',e.scope],['p',e.excludedScope||''],['p',`الحالة: ${states[e.status]||e.status}`],['p',e.sourceCitation||'']]){const el=document.createElement(tag);el.textContent=String(text ?? '').replace(/MARW/g, 'مصدرنا الرسمي');card.append(el);}
    const labels={badal:'البدل',linMahmuz:'اللين المهموز',yaSin:'ياء يس',nunBeforeWaw:'النون قبل الواو',maUndhira:'ما أنذر',abaahum:'آباءهم'};
    const components=document.createElement('ul');Object.entries(e.components).forEach(([key,value])=>{const li=document.createElement('li');li.textContent=`${labels[key]||key}: ${value}`;components.append(li);});card.append(components);
    if(e.sourceBooks){const books=document.createElement('p');books.textContent=`عزو المصدر للتركيب ${e.sourceBranch}: ${e.sourceBooks.join('، ')}`;card.append(books);}
    e.sourceIds.forEach(id=>{const s=data.sources.find(s=>s.id===id),a=document.createElement('a');a.href=s.url;a.textContent=`${s.title} — ${s.locator}`;a.target='_blank';a.rel='noopener';card.append(a);});list.append(card);
   });
  }
  route.onchange=render;search.oninput=render;render();
 }).catch(()=>status.textContent='تعذر تحميل المرجع.');
})();
