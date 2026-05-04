#!/usr/bin/env python3
"""Check what's in the migrated SQLite database."""
import sqlite3, sys, os

if len(sys.argv) < 2:
    # Try to find the default location
    localappdata = os.environ.get('LOCALAPPDATA', '')
    default = os.path.join(localappdata, 'PdTracker', 'settings.json')
    print(f"Usage: python check_sqlite.py <path-to.sqlite>")
    print(f"Or check settings at: {default}")
    if os.path.exists(default):
        import json
        with open(default) as f:
            s = json.load(f)
        path = s.get('Database', {}).get('Path', '')
        if path:
            print(f"Settings points to: {path}")
    else:
        print(f"Settings file not found at: {default}")
    sys.exit(1)

db_path = sys.argv[1]
if not os.path.exists(db_path):
    print(f"FILE NOT FOUND: {db_path}")
    sys.exit(1)

conn = sqlite3.connect(db_path)
cur = conn.cursor()

# Count rows in main tables
tables = ['DEFENDANT', 'QUALIFY', 'ATTORNEY_LIST', 'CHARGE', 'WARRANT',
           'APPOINTMENT', 'VOUCHER', 'DEF_ADDRESS', 'DEF_PHONE', 'EIA']
print(f"Database: {db_path}\n")
for t in tables:
    try:
        cur.execute(f"SELECT COUNT(*) FROM {t}")
        cnt = cur.fetchone()[0]
        print(f"  {t}: {cnt} rows")
    except Exception as e:
        print(f"  {t}: ERROR - {e}")

# Sample defendant names
print("\nSample defendants (first 5):")
try:
    cur.execute("SELECT DefendantID, LastName, FirstName FROM DEFENDANT LIMIT 5")
    for row in cur.fetchall():
        print(f"  {row}")
except Exception as e:
    print(f"  ERROR: {e}")

conn.close()
