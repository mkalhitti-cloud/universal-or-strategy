"""
Analyze MES trade executions from NT8 SQLite database.
Focus: T1 -> T2 conversion rate, average win/loss, stop usage.
NT8 time = .NET ticks (100ns per tick, epoch 0001-01-01)
Convert: unix_sec = (net_ticks - 621355968000000000) / 10_000_000
"""
import sqlite3
import os
from datetime import datetime, timezone
from collections import defaultdict

EPOCH_OFFSET = 621355968000000000  # .NET ticks to Unix epoch
TICKS_PER_SEC = 10_000_000

def net_ticks_to_dt(ticks):
    if ticks <= 0:
        return None
    try:
        unix_sec = (ticks - EPOCH_OFFSET) / TICKS_PER_SEC
        return datetime.fromtimestamp(unix_sec, tz=timezone.utc)
    except (OSError, OverflowError, ValueError):
        return None

db = os.path.expanduser('~/Documents/NinjaTrader 8/db/NinjaTrader.sqlite')
conn = sqlite3.connect(db)
c = conn.cursor()

# --- Find MES instrument IDs ---
c.execute("SELECT Id, Name FROM MasterInstruments WHERE Name LIKE '%MES%'")
mes_masters = c.fetchall()
print('MES MasterInstruments:', mes_masters)

c.execute("SELECT Id, MasterInstrument FROM Instruments WHERE MasterInstrument IN ({})".format(
    ','.join(str(r[0]) for r in mes_masters) if mes_masters else '0'
))
mes_inst_ids = [r[0] for r in c.fetchall()]
print(f'MES Instrument IDs: {mes_inst_ids[:10]}... ({len(mes_inst_ids)} total)')

if not mes_inst_ids:
    # Try by name substring in Executions
    c.execute("SELECT DISTINCT Instrument FROM Executions LIMIT 20")
    all_insts = [r[0] for r in c.fetchall()]
    print('All instrument IDs in Executions:', all_insts[:20])
    
    # Check MasterInstruments for ES/MES
    c.execute("SELECT Id, Name FROM MasterInstruments WHERE Name IN ('MES','ES','NQ','MNQ') LIMIT 20")
    print('ES/MES masters:', c.fetchall())
    
    # Check all distinct instrument IDs in Executions and cross ref
    c.execute("""
        SELECT DISTINCT e.Instrument, mi.Name 
        FROM Executions e
        LEFT JOIN Instruments i ON e.Instrument = i.Id
        LEFT JOIN MasterInstruments mi ON i.MasterInstrument = mi.Id
        LIMIT 30
    """)
    print('Executions instrument breakdown:')
    for r in c.fetchall():
        print(f'  InstrID={r[0]}, Master={r[1]}')
    conn.close()
    exit()

# --- Pull all MES executions ---
placeholders = ','.join('?' * len(mes_inst_ids))
c.execute(f"""
    SELECT e.Id, e.Account, e.Name, e.IsEntry, e.IsExit, 
           e.Price, e.Quantity, e.Time, e.MarketPosition,
           a.Name as AcctName
    FROM Executions e
    LEFT JOIN Accounts a ON e.Account = a.Id
    WHERE e.Instrument IN ({placeholders})
    ORDER BY e.Account, e.Time
""", mes_inst_ids)

rows = c.fetchall()
conn.close()

print(f'\n=== MES Executions: {len(rows)} total ===')

# Convert times and display sample
print('\nSample executions:')
print(f"{'Time UTC':<22} {'Acct':<12} {'Name':<35} {'Qty':>4} {'Price':>8} {'IsEntry':>8} {'IsExit':>7}")
print('-'*100)
for r in rows[:20]:
    dt = net_ticks_to_dt(r[7])
    ts = dt.strftime('%Y-%m-%d %H:%M:%S') if dt else 'INVALID'
    print(f"{ts:<22} {str(r[9] or r[1]):<12} {str(r[2]):<35} {r[6]:>4} {r[5]:>8.2f} {r[3]:>8} {r[4]:>7}")

# --- Group by account and session to identify trade groups ---
# A "trade" = entry followed by exits (T1, T2, Stop, Close, etc.)
# Strategy: group consecutive executions by account where IsEntry=1 starts a new trade

print('\n\n=== EXIT NAME ANALYSIS ===')
exit_names = defaultdict(int)
entry_names = defaultdict(int)
for r in rows:
    if r[4]:  # IsExit
        exit_names[str(r[2])] += 1
    if r[3]:  # IsEntry
        entry_names[str(r[2])] += 1

print('Exit names (how trades closed):')
for name, cnt in sorted(exit_names.items(), key=lambda x: -x[1]):
    print(f'  {cnt:4d}  {name}')

print('\nEntry names:')
for name, cnt in sorted(entry_names.items(), key=lambda x: -x[1]):
    print(f'  {cnt:4d}  {name}')

# --- T1/T2 detection ---
# Look for executions with "Target" in name
print('\n\n=== TARGET EXITS ===')
target_exits = [(r[1], r[2], r[6], r[5], r[7]) for r in rows if 'Target' in str(r[2]) or 'target' in str(r[2])]
print(f'Total target exits: {len(target_exits)}')
target_name_counts = defaultdict(int)
for r in target_exits:
    target_name_counts[r[1]] += 1  # r[1] is Name here
# Re-run with correct index
target_name_counts2 = defaultdict(int)
for r in rows:
    name = str(r[2])
    if 'Target' in name or 'target' in name or 'PTT' in name:
        target_name_counts2[name] += 1

print('Target/PTT exit names:')
for name, cnt in sorted(target_name_counts2.items(), key=lambda x: -x[1]):
    print(f'  {cnt:4d}  {name}')

# --- Stop exits ---
print('\n=== STOP EXITS ===')
stop_counts = defaultdict(int)
for r in rows:
    name = str(r[2])
    if 'Stop' in name or 'stop' in name:
        stop_counts[name] += 1
for name, cnt in sorted(stop_counts.items(), key=lambda x: -x[1]):
    print(f'  {cnt:4d}  {name}')

# --- Build trade PnL ---
# Group by account, find entry then exit sequence
# Simple method: for each entry, find subsequent exits until position=0
print('\n\n=== TRADE PnL RECONSTRUCTION ===')

by_account = defaultdict(list)
for r in rows:
    by_account[r[1]].append(r)

trade_results = []
for acct_id, execs in by_account.items():
    position = 0
    entry_price = 0
    entry_qty = 0
    trade_exits = []
    trade_start_time = None
    
    for r in sorted(execs, key=lambda x: x[7]):
        is_entry = r[3]
        is_exit = r[4]
        qty = r[6]
        price = r[5]
        name = str(r[2])
        dt = net_ticks_to_dt(r[7])
        mkt_pos = r[8]  # 0=long, 1=short, -1=flat
        
        if is_entry and position == 0:
            # New trade starts
            position = qty
            entry_price = price
            entry_qty = qty
            trade_exits = []
            trade_start_time = dt
        elif is_exit and position > 0:
            trade_exits.append({'name': name, 'qty': qty, 'price': price, 'dt': dt})
            position -= qty
            if position <= 0:
                position = 0
                # Compute PnL
                pnl = sum((e['price'] - entry_price) * e['qty'] * 5 for e in trade_exits)
                # Classify
                exit_names_in_trade = [e['name'] for e in trade_exits]
                has_t1 = any('Target' in n and '1' in n or 'T1' in n or 'PTT-BE' in n for n in exit_names_in_trade)
                has_t2 = any('Target' in n and ('2' in n or 'T2' in n) for n in exit_names_in_trade)
                has_stop = any('Stop' in n for n in exit_names_in_trade)
                
                trade_results.append({
                    'acct': acct_id,
                    'entry_price': entry_price,
                    'entry_qty': entry_qty,
                    'pnl': pnl,
                    'exits': exit_names_in_trade,
                    'has_t1': has_t1,
                    'has_t2': has_t2,
                    'has_stop': has_stop,
                    'dt': trade_start_time,
                })

print(f'Reconstructed trades: {len(trade_results)}')
wins = [t for t in trade_results if t['pnl'] > 0]
losses = [t for t in trade_results if t['pnl'] < 0]
breakevens = [t for t in trade_results if t['pnl'] == 0]

print(f'  Wins:       {len(wins)} ({len(wins)/max(len(trade_results),1)*100:.1f}%)')
print(f'  Losses:     {len(losses)} ({len(losses)/max(len(trade_results),1)*100:.1f}%)')
print(f'  Breakevens: {len(breakevens)}')

if wins:
    avg_win = sum(t['pnl'] for t in wins) / len(wins)
    print(f'  Avg win:    ${avg_win:.2f}')
if losses:
    avg_loss = sum(t['pnl'] for t in losses) / len(losses)
    print(f'  Avg loss:   ${avg_loss:.2f}')

total_pnl = sum(t['pnl'] for t in trade_results)
print(f'  Total PnL:  ${total_pnl:.2f}')

# T1/T2 conversion
t1_trades = [t for t in trade_results if t['has_t1']]
t2_trades = [t for t in trade_results if t['has_t2']]
t1_and_t2 = [t for t in trade_results if t['has_t1'] and t['has_t2']]
print(f'\nT1 exits detected: {len(t1_trades)}')
print(f'T2 exits detected: {len(t2_trades)}')
print(f'Both T1 and T2:    {len(t1_and_t2)}')
if t1_trades:
    print(f'T1->T2 conv rate:  {len(t1_and_t2)/len(t1_trades)*100:.1f}%')

print('\nSample trade exits (first 15 trades):')
for t in trade_results[:15]:
    dt_str = t['dt'].strftime('%m-%d %H:%M') if t['dt'] else '?'
    print(f"  {dt_str}  PnL=${t['pnl']:>8.2f}  qty={t['entry_qty']}  exits={t['exits']}")
