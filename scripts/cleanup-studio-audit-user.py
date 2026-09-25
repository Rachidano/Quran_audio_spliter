import importlib.util
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('audit', root / 'scripts/audit-current-corpus.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
email = sys.argv[1]
assert email.startswith('studio-audit-') and email.endswith('@local.invalid')
with audit.connect() as conn, conn.cursor() as cursor:
    cursor.execute('DELETE FROM "AspNetUsers" WHERE "Email"=%s', (email,))
