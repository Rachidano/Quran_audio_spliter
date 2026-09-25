"""Read-only database audit. Uses the application's local connection configuration."""
import json
import pathlib
import sys
import psycopg2

ROOT = pathlib.Path(__file__).resolve().parents[1]

def connect():
    config = json.loads((ROOT / 'src/Quran.Web/appsettings.json').read_text(encoding='utf-8-sig'))
    parts = dict(p.split('=', 1) for p in config['ConnectionStrings']['DefaultConnection'].strip(';').split(';'))
    return psycopg2.connect(host=parts['Host'], port=parts.get('Port', 5432), dbname=parts['Database'], user=parts['Username'], password=parts['Password'])

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    with connect() as conn, conn.cursor() as cur:
        for query in [
            'SELECT "Code", "QualificationStatus", "IsActive" FROM "QuranCorpora" ORDER BY "Id"',
            'SELECT "CorpusId",count(*),count(*) FILTER (WHERE "TextArabic" LIKE \'[%%\') FROM "CorpusAyahs" GROUP BY "CorpusId"',
            'SELECT "Id","Reference","Edition","StartMs","EndMs","Status","CreatedByUserId" FROM "AudioSegments" ORDER BY "StartMs"',
            'SELECT "SourceReference","TargetReference","ReviewStatus","ReviewedByUserId" FROM "ReferenceMappings"',
            'SELECT count(*) FROM "Ayahs"',
        ]:
            cur.execute(query)
            print(json.dumps(cur.fetchall(), ensure_ascii=False, default=str))
