import sqlite3
import os
from datetime import datetime, timezone
from collections import defaultdict

db_path = os.path.expanduser('~/Documents/NinjaTrader 8/db/NinjaTrader.sqlite')
conn = sqlite3.connect(db_path)
c = conn.cursor()

# .NET ticks to datetime: ticks since 0001-01-01, 1 tick = 100ns
DOTNET_EPOCH = datetime(1, 1, 1, tzinfo=timezone.utc)
def ticks_to_dt(ticks):
    try:
        return DOTNET_EPOCH + __import__('datetime').timedelta(microseconds=ticks // 10)
    except:
        return None

# Get MES executions since July 21 2026
# First find MES instrument IDs
c.execute("SELECT Id, Name FROM MasterInstruments WHERE Name LIKE '%MES%'")
mes_instruments = c.fetchall()
print("MES Instruments:", mes_instruments)

# Get all executions with account and instrument info
c.execute("""
    SELECT e.Id, e.Account, e.IsEntry, e.IsExit, e.Name, e.Price, e.Quantity, 
           e.Time, e.StatementDate, e.MarketPosition,
           a.Name as AccountName, mi.Name as InstrumentName
    FROM Executions e
    JOIN Accounts a ON e.Account = a.Id
    JOIN MasterInstruments mi ON e.Instrument = mi.Id
    WHERE mi.Name LIKE '%MES%'
    ORDER BY e.Time ASC
""")
all_exec = c.fetchall()

print(f"\nTotal MES executions: {len(all_exec)}")

# Filter since July 21 2026
cutoff = datetime(2026, 7, 21, tzinfo=timezone.utc)
recent = []
for row in all_exec:
    dt = ticks_to_dt(row[7])
    if dt and dt >= cutoff:
        recent.append(row + (dt,))

print(f"Since July 21 2026: {len(recent)}")
print()

# Show date range
if recent:
    print(f"Date range: {recent[0][-1].strftime('%Y-%m-%d %H:%M')} to {recent[-1][-1].strftime('%Y-%m-%d %H:%M')}")
    print()

# Get unique accounts
accounts = set(r[10] for r in recent)
print(f"Accounts: {sorted(accounts)}")
print()

# Group by account and analyze trade outcomes
# A trade = entry + all exits until flat
# IsEntry=1 = opening, IsExit=1 = closing
# Name field contains order type (Entry, Target1, Target2, Stop1, Stop2, Close, PTT-BE-Target, etc.)

# Analyze by looking at entry/exit pairs
print("=== EXECUTION BREAKDOWN (first 30 MES since July 21) ===")
print(f"{'Time':<20} {'Acct':<8} {'Name':<25} {'Qty':>4} {'Price':>8} {'Entry':>6} {'Exit':>5}")
print("-" * 80)
for row in recent[:30]:
    dt = row[-1]
    print(f"{dt.strftime('%m/%d %H:%M:%S'):<20} {row[10]:<8} {str(row[4]):<25} {row[6]:>4} {row[5]:>8.2f} {'YES' if row[2] else '':>6} {'YES' if row[3] else '':>5}")

conn.close()
