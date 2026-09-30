"""
Re-analyze execution data EXCLUDING SL4 (single-target ATM).
SL4 had only 1 target in old setup -- its T1 exits were final exits, not partial fills.
We want clean T1->T2 conversion stats from SL5-SL8 only (2-target ATMs).

Method to identify SL4 trades: entry qty = 7 (old $200 SL6 = qty 7 per CSV sample)
Actually we don't know exact SL per trade from executions alone.
Best proxy: infer SL from the actual stop loss that fired, or from trade loss size.
For trades that hit hard stop: implied_sl = |pnl| / (qty * 5)
For T1+T2 ATMs: we expect to see both Target1 AND Target2 exits in same trade.
Single-target ATM trades: only ever see Target1 as the exit (no Target2 possible).

Strategy:
1. Identify trades that had ONLY Target1 as exit (no T2 ever fired on that account/session)
   vs accounts where Target2 did appear at least sometimes.
2. Split by implied stop distance to separate SL4 (small stop) from SL5+ (larger stop).
3. Show T1->T2 conversion for multi-target trades only.
"""
import sqlite3
import os
from datetime import datetime, timezone
from collections import defaultdict

EPOCH_OFFSET = 621355968000000000
TICKS_PER_SEC = 10_000_000

def net_ticks_to_dt(ticks):
    if not ticks or ticks <= 0:
        return None
    try:
        unix_sec = (ticks - EPOCH_OFFSET) / TICKS_PER_SEC
        return datetime.fromtimestamp(unix_sec, tz=timezone.utc)
    except:
        return None

db = os.path.expanduser('~/Documents/NinjaTrader 8/db/NinjaTrader.sqlite')
conn = sqlite3.connect(db)
c = conn.cursor()

c.execute("SELECT Id FROM MasterInstruments WHERE Name='MES'")
mes_master_ids = [r[0] for r in c.fetchall()]
c.execute(f"SELECT Id FROM Instruments WHERE MasterInstrument IN ({','.join(str(i) for i in mes_master_ids)})")
mes_inst_ids = [r[0] for r in c.fetchall()]

ph = ','.join('?' * len(mes_inst_ids))
c.execute(f"""
    SELECT e.Id, e.Account, e.Name, e.IsEntry, e.IsExit,
           e.Price, e.Quantity, e.Time, e.MarketPosition,
           a.Name as AcctName
    FROM Executions e
    LEFT JOIN Accounts a ON e.Account = a.Id
    WHERE e.Instrument IN ({ph})
    ORDER BY e.Account, e.Time
""", mes_inst_ids)
rows = c.fetchall()
conn.close()

# Rebuild trades
by_account = defaultdict(list)
for r in rows:
    by_account[r[1]].append(r)

trades = []
for acct_id, execs in by_account.items():
    position = 0; entry_px = 0; entry_qty = 0
    entry_time = None; direction = None; exits_buf = []

    for r in sorted(execs, key=lambda x: x[7]):
        is_entry, is_exit = r[3], r[4]
        qty, price, name = r[6], r[5], str(r[2])
        dt = net_ticks_to_dt(r[7])
        mkt_pos = r[8]

        if is_entry and position == 0:
            position = qty; entry_px = price; entry_qty = qty
            entry_time = dt
            direction = 'long' if mkt_pos == 0 else 'short'
            exits_buf = []
        elif is_exit and position > 0:
            exits_buf.append({'name': name, 'qty': qty, 'price': price, 'dt': dt})
            position -= qty
            if position <= 0:
                position = 0
                exit_names = [e['name'] for e in exits_buf]
                def has(kw): return any(kw.lower() in n.lower() for n in exit_names)
                sign = 1 if direction == 'long' else -1
                pnl = sum(sign*(e['price']-entry_px)*e['qty']*5 for e in exits_buf)
                hit_t1 = has('target1') or has('t1_') or has('ptt-qx-t1')
                hit_t2 = has('target2') or has('t2_') or has('ptt-qx-t2')
                hit_stop = has('stop') and not has('ptt-be-stop')
                hit_be = has('ptt-be-stop')
                manual = has('close') or has('graceful') or has('flatten') or has('external')
                implied_sl = round(abs(pnl) / (entry_qty * 5), 2) if pnl < 0 else None

                trades.append({
                    'acct': acct_id, 'entry_qty': entry_qty, 'direction': direction,
                    'pnl': pnl, 'exits': exit_names,
                    'hit_t1': hit_t1, 'hit_t2': hit_t2,
                    'hit_stop': hit_stop, 'hit_be': hit_be, 'manual': manual,
                    'implied_sl': implied_sl,
                    'entry_time': entry_time,
                })

print(f"Total trades: {len(trades)}")
print()

# ── Infer ATM type from quantity ─────────────────────────────────────────────
# Old $200 ATMs:
#   SL4 = floor(200/20) = 10 contracts  (single target)
#   SL5 = floor(200/25) = 8 contracts
#   SL6 = floor(200/30) = 6... but CSV showed qty=7
# New $400 ATMs:
#   SL4 = 20, SL5 = 16, SL6 = 13, SL7 = 11, SL8 = 10
# Current data is $200-era, so quantities are half.
# Most common quantities:
qty_counts = defaultdict(int)
for t in trades:
    qty_counts[t['entry_qty']] += 1
print("Entry quantity distribution:")
for qty, cnt in sorted(qty_counts.items(), key=lambda x: -x[1]):
    print(f"  qty={qty:3d}: {cnt:4d} trades  ({cnt/len(trades)*100:.1f}%)")
print()

# Map $200-era quantities to implied SL
# qty=7 -> SL6 (7*6*5=$210 risk), qty=8 -> SL5 (8*5*5=$200), qty=10 -> SL4 (10*4*5=$200)
# But also single-target SL4 would show qty=7 too if it was $200/30=6.6->7 for SL6
# Better: use implied_sl from stop losses that fired
print("Implied SL distribution (from hard stop trades only):")
stop_trades = [t for t in trades if t['hit_stop'] and t['implied_sl'] is not None]
sl_counts = defaultdict(int)
for t in stop_trades:
    sl_rounded = round(t['implied_sl'] * 4) / 4  # round to nearest 0.25
    sl_counts[sl_rounded] += 1
for sl, cnt in sorted(sl_counts.items()):
    print(f"  implied SL={sl:.2f}pts: {cnt:3d} trades")
print()

# ── Classify trades by implied SL bucket ─────────────────────────────────────
# Use implied SL for stop trades; for non-stop trades use qty proxy
def classify_sl(t):
    if t['implied_sl'] is not None:
        s = t['implied_sl']
        if s <= 4.5:   return 4
        elif s <= 5.5: return 5
        elif s <= 6.5: return 6
        elif s <= 7.5: return 7
        else:          return 8
    # fallback: qty-based (for $200-era)
    q = t['entry_qty']
    if q >= 18:   return 4   # $400 SL4
    elif q >= 14: return 5
    elif q >= 11: return 6
    elif q >= 9:  return 7
    elif q >= 8:  return 8
    elif q >= 7:  return 6   # $200-era SL6
    elif q >= 6:  return 7
    else:          return 8

for t in trades:
    t['inferred_sl'] = classify_sl(t)

# ── Show which SLs had Target2 exits at all ───────────────────────────────────
print("=" * 80)
print("  TARGET2 PRESENCE BY INFERRED SL")
print("=" * 80)
for sl in [4, 5, 6, 7, 8]:
    sl_trades = [t for t in trades if t['inferred_sl'] == sl]
    t1_trades = [t for t in sl_trades if t['hit_t1']]
    t2_trades = [t for t in sl_trades if t['hit_t2']]
    both      = [t for t in sl_trades if t['hit_t1'] and t['hit_t2']]
    conv = len(both)/max(len(t1_trades),1)*100
    print(f"  SL{sl}: {len(sl_trades):4d} total  |  T1 hits: {len(t1_trades):3d}  "
          f"T2 hits: {len(t2_trades):3d}  Both: {len(both):3d}  "
          f"T1->T2 conv: {conv:5.1f}%")
print()

# ── KEY: filter out SL4 (single-target) and recompute ─────────────────────────
multi_target = [t for t in trades if t['inferred_sl'] in [5, 6, 7, 8]]
t1_mt  = [t for t in multi_target if t['hit_t1']]
t2_mt  = [t for t in multi_target if t['hit_t2']]
both_mt = [t for t in multi_target if t['hit_t1'] and t['hit_t2']]

print("=" * 80)
print("  MULTI-TARGET TRADES ONLY (SL5-SL8, excluding single-target SL4)")
print("=" * 80)
n = len(multi_target)
full_wins    = [t for t in multi_target if t['hit_t1'] and t['hit_t2']]
t1_only      = [t for t in multi_target if t['hit_t1'] and not t['hit_t2']]
be_exits     = [t for t in multi_target if t['hit_be'] and not t['hit_t1']]
stop_exits   = [t for t in multi_target if t['hit_stop'] and not t['hit_t1']]
manual_exits = [t for t in multi_target if t['manual'] and not t['hit_t1'] and not t['hit_t2']]

print(f"  Trades analyzed (SL5-SL8): {n}")
print(f"  Full win  (T1+T2):      {len(full_wins):4d}  ({len(full_wins)/n*100:5.1f}%)")
print(f"  T1 only   (T2 missed):  {len(t1_only):4d}  ({len(t1_only)/n*100:5.1f}%)")
print(f"  BE stop:                {len(be_exits):4d}  ({len(be_exits)/n*100:5.1f}%)")
print(f"  Hard stop:              {len(stop_exits):4d}  ({len(stop_exits)/n*100:5.1f}%)")
print(f"  Manual close:           {len(manual_exits):4d}  ({len(manual_exits)/n*100:5.1f}%)")
print()
print(f"  T1 hit rate:            {len(t1_mt)/n*100:.1f}%")
print(f"  T2 hit rate:            {len(t2_mt)/n*100:.1f}%")
print(f"  T1->T2 conversion:      {len(both_mt)/max(len(t1_mt),1)*100:.1f}%")
print()

if full_wins:
    avg_win_full = sum(t['pnl'] for t in full_wins)/len(full_wins)
    print(f"  Avg full win PnL:  ${avg_win_full:+.2f}")
if t1_only:
    avg_t1_only = sum(t['pnl'] for t in t1_only)/len(t1_only)
    print(f"  Avg T1-only PnL:   ${avg_t1_only:+.2f}")
if stop_exits:
    avg_stop = sum(t['pnl'] for t in stop_exits)/len(stop_exits)
    print(f"  Avg hard stop PnL: ${avg_stop:+.2f}")
if manual_exits:
    avg_manual = sum(t['pnl'] for t in manual_exits)/len(manual_exits)
    print(f"  Avg manual PnL:    ${avg_manual:+.2f}")

total_pnl = sum(t['pnl'] for t in multi_target)
print(f"  Total PnL (SL5-8): ${total_pnl:+.2f}")
print()

# ── For comparison: SL4 alone ─────────────────────────────────────────────────
sl4_trades = [t for t in trades if t['inferred_sl'] == 4]
print("=" * 80)
print("  SL4 TRADES ALONE (single-target, for reference)")
print("=" * 80)
if sl4_trades:
    n4 = len(sl4_trades)
    t1_4 = [t for t in sl4_trades if t['hit_t1']]
    t2_4 = [t for t in sl4_trades if t['hit_t2']]
    print(f"  Trades: {n4}  |  T1 hits: {len(t1_4)} ({len(t1_4)/n4*100:.1f}%)  "
          f"T2 hits: {len(t2_4)} (expected ~0 since single target)")
    print(f"  Total PnL: ${sum(t['pnl'] for t in sl4_trades):+.2f}")
else:
    print("  No SL4 trades identified (all may be SL6-era $200 qty=7)")
print()

# ── What T2 was set to in old ATMs (infer from T2 exit prices) ────────────────
print("=" * 80)
print("  OLD T2 EXIT PRICES (what was T2 actually set to?)")
print("=" * 80)
t2_exit_trades = [t for t in trades if t['hit_t2']]
print(f"  Trades with T2 fills: {len(t2_exit_trades)}")
# We can't directly get T2 price from execution data without entry price context
# But we can get implied T2 distance = pnl_on_t2_contracts
# For full wins: pnl = T1*qty_t1*5 + T2*qty_t2*5 - fees
# If we assume 50/50 split and know entry_qty and pnl:
print("  (T2 distance inferred from full-win PnL where both T1 and T2 filled)")
for t in full_wins[:15]:
    qt = t['entry_qty']
    qt1 = qt // 2
    qt2 = qt - qt1
    fees = 0.57 * qt
    # pnl = t1_dist*qt1*5 + t2_dist*qt2*5 - fees
    # we don't know t1_dist exactly, but if T1 was 50%SL and SL=implied...
    sl_est = t['inferred_sl']
    t1_est = sl_est * 0.50
    t2_implied = (t['pnl'] + fees - t1_est*qt1*5) / (qt2*5) if qt2 > 0 else None
    dt_str = t['entry_time'].strftime('%m-%d %H:%M') if t['entry_time'] else '?'
    print(f"  {dt_str}  qty={qt}  SL~{sl_est}  PnL={t['pnl']:>+8.2f}  "
          f"implied T2~{t2_implied:.2f}pts" if t2_implied else f"  {dt_str}  PnL={t['pnl']:>+8.2f}")
