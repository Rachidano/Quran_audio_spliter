from pathlib import Path
import json,hashlib,struct,collections
root=Path('data/mushaf/editions/marw-newMushaf/20260913-full')
rows=[];errors=[];continuations=[];bounds=[]
for n in range(1,605):
 p=root/f'page-{n:03}.json'
 if not p.exists(): errors.append(f'missing {n}');continue
 j=json.loads(p.read_text(encoding='utf-8-sig'));assert j['page']==n and j['mushafId']=='newMushaf'
 im=(root/f'page-{n:03}.png').read_bytes();assert im[:8]==bytes([137,80,78,71,13,10,26,10]);assert min(struct.unpack('>II',im[16:24]))>0
 c=j['boxes']['cropBox'];t=j['boxes']['textBox']
 for r in j['rows']:
  rows.append((n,r['verseNo'],r['surah'],r['ayah']))
  if r['continuousFromPrevPage']:continuations.append((n,r['surah'],r['ayah']))
  for b in r['rects']:
   x=(c['left']+c['width']*(t['left']+t['width']*b['left']/100)/100)/100
   y=(c['top']+c['height']*(t['top']+t['height']*b['top']/100)/100)/100
   w=c['width']*t['width']*b['width']/1000000;h=c['height']*t['height']*b['height']/1000000
   if min(x,y)<0 or min(w,h)<=0 or x+w>1.000001 or y+h>1.000001:bounds.append((n,r['surah'],r['ayah'],x,y,w,h))
refs={v:(s,a) for p,v,s,a in rows}
for prev,cur in zip(rows,rows[1:]):
 if cur[1] not in (prev[1],prev[1]+1):errors.append(f'jump {prev} -> {cur}')
for s in range(1,115):
 ayahs=sorted(set(a for p,v,su,a in rows if su==s))
 if not ayahs or ayahs!=list(range(1,max(ayahs)+1)):errors.append(f'noncontiguous surah {s}')
report=dict(pages=604,uniqueAyat=len(refs),surahs=len(set(s for p,v,s,a in rows)),rows=len(rows),continuations=continuations,boundsErrors=bounds,sequenceErrors=errors,first=rows[0],last=rows[-1])
Path('artifacts/marw-full-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
assert not errors and not bounds and sorted(refs)==list(range(1,6215))
