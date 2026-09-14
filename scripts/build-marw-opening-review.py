"""Package manually observed first-text-line indices; never infer them from verse ends."""
from pathlib import Path
import json, hashlib

root = Path(__file__).resolve().parent.parent
source = root / 'data/mushaf/editions/marw-newMushaf/20260913-full'
starts = json.loads((root / 'scripts/marw-reviewed-start-lines.json').read_text())
assert len(starts) == 114 and all(isinstance(n, int) for n in starts)
entries = []
for page in range(1, 605):
    raw = (source / f'page-{page:03}.json').read_bytes()
    map_data = json.loads(raw)
    for row in map_data['rows']:
        if row['ayah'] != 1:
            continue
        surah = row['surah']
        count = 7 if page <= 2 else 15
        first = starts[surah - 1]
        assert 0 <= first < count
        assert any(r['top'] + r['height'] > first * 100 / count + 1e-8 for r in row['rects'])
        entries.append(dict(Surah=surah, Page=page, LineCount=count, FirstTextLine=first,
                            MapSha256=hashlib.sha256(raw).hexdigest(),
                            ImageSha256=hashlib.sha256((source / f'page-{page:03}.png').read_bytes()).hexdigest()))
assert [e['Surah'] for e in entries] == list(range(1, 115))
report = dict(Revision='20260913-full', ReviewVersion='openings-v1',
              Scope='First ayah only: visual line coverage and end marker; not textual or religious certification.',
              Reviewer='Codex visual inspection', Entries=entries)
target = root / 'src/Quran.Infrastructure/Data/marw-opening-review.json'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(f'Packaged {len(entries)} visually observed opening boundaries.')
