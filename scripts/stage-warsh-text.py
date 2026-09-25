"""Stage a pinned Warsh text candidate; never overwrite Ayahs or approve mappings.

Usage: python scripts/stage-warsh-text.py [--apply]
Requires psycopg2 and the local application's configured database.
"""
import datetime
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import importlib.util

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('audit', ROOT / 'scripts/audit-current-corpus.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
sys.stdout.reconfigure(encoding='utf-8')
folder = ROOT / 'data/reference/warsh-qpc-v10'
raw = (folder / 'source.json').read_bytes()
manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8-sig'))
assert hashlib.sha256(raw).hexdigest().upper() == manifest['sha256'].upper()
rows = json.loads(raw.decode('utf-8-sig'))
marw = {}
for path in (ROOT / 'data/mushaf/editions/marw-newMushaf/20260913-full').glob('page-*.json'):
    page = json.loads(path.read_text(encoding='utf-8-sig'))
    for r in page['rows']:
        marw[(r['surah'], r['ayah'])] = page['page']
keys = {(r['sura_no'], r['aya_no']) for r in rows}
assert len(rows) == len(keys) == len(marw) == 6214
assert keys == set(marw), 'Reference sets differ; abort'
assert len({r['sura_no'] for r in rows}) == 114
differences = [dict(reference=f"{r['sura_no']}:{r['aya_no']}", qpcPage=r['page'], marwPage=marw[(r['sura_no'], r['aya_no'])])
               for r in rows if str(r['page']) != str(marw[(r['sura_no'], r['aya_no'])])]
report = dict(rows=len(rows), referenceSetMatches=True, pageDifferences=differences,
              qualification='Candidate: structural comparison only; no exhaustive PDF/text/audio validation',
              sha256=manifest['sha256'], repositoryRevision=manifest['revision'])
(folder / 'comparison.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'6214 references, 114 surahs, {len(differences)} page metadata differences; candidate remains separate.')
if '--apply' not in sys.argv:
    sys.exit(0)

with audit.connect() as conn:
    # Full backup before any content mutation. No credentials printed or passed in argv.
    backup = ROOT / 'backups' / ('corpus-audit-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    backup.mkdir(parents=True, exist_ok=False)
    import os
    params = conn.get_dsn_parameters()
    env = os.environ.copy()
    cfg = json.loads((ROOT / 'src/Quran.Web/appsettings.json').read_text(encoding='utf-8-sig'))
    credentials = dict(p.split('=', 1) for p in cfg['ConnectionStrings']['DefaultConnection'].strip(';').split(';'))
    env['PGPASSWORD'] = credentials['Password']
    subprocess.run([r'D:\Program Files\PostgreSQL\16\bin\pg_dump.exe', '-h', params['host'], '-p', params['port'],
                    '-U', params['user'], '-d', params['dbname'], '-Fc', '-f', str(backup / 'database.dump')], env=env, check=True)
    with conn.cursor() as cur:
        cur.execute('SELECT "Id" FROM "QuranCorpora" WHERE "Code"=%s', ('qpc-warsh-v10-candidate',))
        existing = cur.fetchone()
        if existing:
            cur.execute('SELECT count(*) FROM "CorpusAyahs" WHERE "CorpusId"=%s', existing)
            assert cur.fetchone()[0] == 6214, 'Partial existing import; investigate'
            print('Candidate already present; no overwrite.')
        else:
            cur.execute('''INSERT INTO "QuranCorpora" ("Code","NameArabic","NameLatin","RiwayaCode","CountingSystem","TotalAyahs","TextualRevision","Provenance","QualificationStatus","IsActive","CreatedAt")
                VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,false,now()) RETURNING "Id"''',
                ('qpc-warsh-v10-candidate', 'نص ورش — نسخة للمقارنة والمراجعة', 'QPC Warsh v10 — candidate mirror', 'warsh',
                 '6214-reference-set-matches-MARW', 6214, manifest['revision'], json.dumps(manifest), 'PendingTextReview'))
            corpus_id = cur.fetchone()[0]
            for row in rows:
                # Keep the supplier text unchanged. Search text is a derived field only.
                text = row['aya_text']
                normalized = re.sub(r'[\u064b-\u065f\u0670\u06d6-\u06ed]', '', text)
                normalized = re.sub('[إأآٱ]', 'ا', normalized)
                cur.execute('''INSERT INTO "CorpusAyahs" ("CorpusId","SurahNumber","AyahNumber","NumberInQuran","TextArabic","SearchText","PageNumberHint")
                    VALUES (%s,%s,%s,%s,%s,%s,%s)''',
                    (corpus_id,row['sura_no'],row['aya_no'],row['id'],text,normalized,int(re.split(r'\D+',str(row['page']))[0])))
            print('Imported 6214 candidate texts in a separate inactive corpus. Ayahs and audio annotations untouched.')
        # Undo only machine-generated approvals; do not change actual human decisions.
        cur.execute('''UPDATE "ReferenceMappings" SET "ReviewStatus"='PendingReview', "ReviewedByUserId"=NULL,"ReviewedAt"=NULL
            WHERE "ReviewedByUserId"='SystemAuditor' AND "ReviewStatus"='Approved' ''')
        print('Machine approvals reset:', cur.rowcount)
        cur.execute('''UPDATE "QuranCorpora" SET "NameArabic"='مصحف الجزائر الإلكتروني — MARW', "QualificationStatus"='ImagesImportedTextPending'
            WHERE "Code"='marw-newMushaf' ''')
    print('Backup:', backup.relative_to(ROOT))
