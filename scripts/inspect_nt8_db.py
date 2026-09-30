import sqlite3
import os

db = os.path.expanduser('~/Documents/NinjaTrader 8/db/NinjaTrader.sqlite')
conn = sqlite3.connect(db)
c = conn.cursor()

# List all tables and row counts
c.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
tables = [r[0] for r in c.fetchall()]
print('=== TABLES ===')
for t in tables:
    try:
        c.execute(f'SELECT COUNT(*) FROM "{t}"')
        print(f'  {t}: {c.fetchone()[0]} rows')
    except Exception as e:
        print(f'  {t}: ERROR - {e}')

# Sample from Executions if it exists
if 'Executions' in tables:
    print('\n=== Executions columns ===')
    c.execute('PRAGMA table_info(Executions)')
    for col in c.fetchall():
        print(f'  {col[1]} ({col[2]})')

    print('\n=== Sample Executions rows (first 5) ===')
    c.execute('SELECT * FROM Executions LIMIT 5')
    rows = c.fetchall()
    for r in rows:
        print(r)

# Check Orders table
if 'Orders' in tables:
    print('\n=== Orders columns ===')
    c.execute('PRAGMA table_info(Orders)')
    for col in c.fetchall():
        print(f'  {col[1]} ({col[2]})')
    print('\n=== Sample Orders rows (first 3) ===')
    c.execute('SELECT * FROM Orders LIMIT 3')
    for r in c.fetchall():
        print(r)

conn.close()
