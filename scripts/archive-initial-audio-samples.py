"""Remove only the three identified initial 1.000–6.500s samples, with an archive.
User annotation UUIDs and originals are never selected. Run with --apply to mutate.
"""
import importlib.util
import json
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('audit', root / 'scripts/audit-current-corpus.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
ids = ['a34695f9-431e-4077-b6a1-af58eb7fa496', '811ad6d6-9065-4531-a979-6c44da4d464d', '2385d109-31fc-42ec-86e0-a134905a7321']
with audit.connect() as conn, conn.cursor() as cur:
    cur.execute('SELECT row_to_json(s) FROM "AudioSegments" s WHERE "Id"::text=ANY(%s) FOR UPDATE', (ids,))
    rows = [r[0] for r in cur.fetchall()]
    assert all(r['StartMs'] == 1000 and r['EndMs'] == 6500 and r['Reference'] is None and r['AyahId'] == 10 for r in rows)
    cur.execute('SELECT count(*) FROM "AudioSegments" WHERE "RepeatOfId"::text=ANY(%s)', (ids,))
    assert cur.fetchone()[0] == 0, 'Linked repeats exist; abort'
    print('Identified initial samples:', len(rows))
    if '--apply' in sys.argv and rows:
        backup = root / 'backups/corpus-audit-20260914-162028/initial-audio-samples.json'
        assert (backup.parent / 'database.dump').exists(), 'Full pre-change backup required'
        assert not backup.exists(), 'Do not overwrite the archive'
        backup.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
        for row in rows:
            if row['PublishedAudioAssetId']:
                cur.execute('UPDATE "Recitations" SET "IsPublished"=false,"Status"=1 WHERE "AudioAssetId"=%s', (row['PublishedAudioAssetId'],))
            cur.execute('''INSERT INTO "AuditLogs" ("UserId","Action","EntityType","EntityId","OldValuesJson","CreatedAt")
                VALUES ('maintenance-user-request','ArchiveInitialAudioSample','AudioSegment',%s,%s,now())''', (row['Id'],json.dumps(row)))
        cur.execute('DELETE FROM "AudioSegments" WHERE "Id"::text=ANY(%s)', (ids,))
        print('Archived and removed:', cur.rowcount, '; user annotations and audio files preserved.')
