"""Build a local research comparison from the three downloaded MARW pages.
No network, image alteration, database write or application change.
"""
from pathlib import Path
import json
import hashlib
import struct
from datetime import datetime, timezone

root = Path(__file__).resolve().parents[1]
folder = root / 'docs/research/marw'
pages = []
sources = []
for number in (1, 2, 3):
    name = f'page-{number:03}'
    raw = json.loads((folder / f'{name}.json').read_text(encoding='utf-8-sig'))
    assert raw['page'] == number and raw['mushafId'] == 'newMushaf'
    image = (folder / f'{name}.png').read_bytes()
    assert image[:8] == b'\x89PNG\r\n\x1a\n'
    width, height = struct.unpack('>II', image[16:24])
    crop, text = raw['boxes']['cropBox'], raw['boxes']['textBox']
    regions = []
    for row in raw['rows']:
        boxes = []
        for rect in row['rects']:
            x = (crop['left'] + crop['width'] * (text['left'] + text['width'] * rect['left'] / 100) / 100) / 100
            y = (crop['top'] + crop['height'] * (text['top'] + text['height'] * rect['top'] / 100) / 100) / 100
            w = crop['width'] * text['width'] * rect['width'] / 1000000
            h = crop['height'] * text['height'] * rect['height'] / 1000000
            assert w > 0 and h > 0 and min(x, y) >= 0 and x + w <= 1.000001 and y + h <= 1.000001
            boxes.append(dict(x=x, y=y, width=w, height=h))
        regions.append(dict(reference=f"{row['surah']}:{row['ayah']}", verseNo=row['verseNo'], boxes=boxes))
    pages.append(dict(number=number, width=width, height=height, regions=regions))
    for ext, endpoint in [('png', 'pages'), ('json', 'ayat-shadows')]:
        filename = f'{name}.{ext}'
        url = f'https://mushaf.marw.dz/web/api/v1/masahif/newMushaf/{endpoint}/{number}' + ('.png' if ext == 'png' else '')
        sources.append(dict(file=filename, url=url, sha256=hashlib.sha256((folder / filename).read_bytes()).hexdigest()))

assert [len(p['regions']) for p in pages] == [7, 4, 11]
(folder / 'normalized-sample.json').write_text(json.dumps(pages, indent=2), encoding='utf-8')
(folder / 'manifest.json').write_text(json.dumps(dict(
    edition='newMushaf', source='https://mushaf.marw.dz/web/ar/mushaf/newMushaf/1',
    recordedAtUtc=datetime.now(timezone.utc).isoformat(),
    status='Research only; text preview mismatch observed; redistribution terms unconfirmed',
    files=sources), indent=2), encoding='utf-8')

template = '''<!doctype html><html lang="fr"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Comparaison Warsh — source MARW</title>
<style>
body{font:16px system-ui;background:#f4f3ed;color:#193d32;margin:24px}h1{font-size:25px}
.note{background:#fff3cc;padding:16px;border-radius:10px;max-width:1000px}
nav, #refs{display:flex;gap:8px;flex-wrap:wrap;margin:16px 0}button{padding:10px 16px;border:1px solid #9ebbac;border-radius:7px;background:white;cursor:pointer}
button[aria-pressed=true]{background:#175b45;color:white}.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}
.sheet{position:relative;background:white}.sheet img{width:100%;display:block}.sheet svg{position:absolute;inset:0;width:100%;height:100%}
rect{fill:transparent;cursor:pointer}rect:hover,rect.selected{fill:#e3b73b60;stroke:#aa7d12;stroke-width:.1}
h2{font-size:18px}a{color:#175b45}#status{min-height:24px}@media(max-width:700px){.grid{grid-template-columns:1fr}}
</style>
<h1>Warsh : pilote actuel / source MARW « newMushaf »</h1>
<p class="note">Échantillon de recherche. Les images restent inchangées. Les zones de droite reprennent les coordonnées MARW.
Le texte de l’API « preview » présente des divergences avec les images : il n’est pas affiché et aucun audio n’est associé.</p>
<nav id="pages" aria-label="Page"></nav><div id="refs" aria-label="Ayat MARW"></div><p id="status" role="status">Choisir une ayah pour vérifier ses zones.</p>
<div class="grid"><section><h2>Pilote actuel — Quran.ws / KFQC</h2><div class="sheet"><img id="old" alt="Page du pilote actuel"></div></section>
<section><h2>MARW — newMushaf, points orientaux</h2><div class="sheet"><img id="new" alt="Page MARW"><svg id="overlay" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true"></svg></div></section></div>
<p><a href="https://mushaf.marw.dz/web/ar/mushaf/newMushaf/1">Consulter la source MARW</a> · <a href="README.md">Constats et limites</a></p>
<script>
const pages=__DATA__;
const ns='http://www.w3.org/2000/svg';
function select(ref){document.querySelectorAll('#overlay rect').forEach(r=>r.classList.toggle('selected',r.dataset.ref===ref));document.querySelectorAll('#refs button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.ref===ref)));document.getElementById('status').textContent='Référence dans la carte MARW : '+ref;}
function show(number){const page=pages.find(p=>p.number===number);const padded=String(number).padStart(3,'0');document.getElementById('old').src='../warsh-svg-sample/'+padded+'.svg';document.getElementById('new').src='page-'+padded+'.png';document.getElementById('refs').replaceChildren();document.getElementById('overlay').replaceChildren();document.getElementById('status').textContent='Choisir une ayah pour vérifier ses zones.';document.querySelectorAll('#pages button').forEach(b=>b.setAttribute('aria-pressed',String(Number(b.dataset.page)===number)));for(const region of page.regions){const button=document.createElement('button');button.textContent=region.reference;button.dataset.ref=region.reference;button.setAttribute('aria-pressed','false');button.onclick=()=>select(region.reference);document.getElementById('refs').append(button);for(const box of region.boxes){const r=document.createElementNS(ns,'rect');for(const [k,v] of Object.entries(box))r.setAttribute(k,String(v*100));r.dataset.ref=region.reference;r.onclick=()=>select(region.reference);document.getElementById('overlay').append(r);}}}
for(const p of pages){const b=document.createElement('button');b.textContent='Page '+p.number;b.dataset.page=p.number;b.onclick=()=>show(p.number);document.getElementById('pages').append(b);}show(2);
</script></html>'''
(folder / 'comparison.html').write_text(template.replace('__DATA__', json.dumps(pages)), encoding='utf-8')
print('3 pages, 22 ayat; all rectangles fit the original images. comparison.html created.')
