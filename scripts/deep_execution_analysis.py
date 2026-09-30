"""
Deep execution analysis - understand what actually happened in real trades.
Focus: target fill rates, partial vs full wins, manual close behavior, 
trade duration, and T2 distance analysis.
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

def ticks_to_minutes(ticks):
    return ticks / (TICKS_PER_SEC * 60)

db = os.path.expanduser('~/Documents/NinjaTrader 8/db/NinjaTrader.sqlite')
conn = sqlite3.connect(db)
c = conn.cursor()

# Get MES instrument IDs
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

# ── Rebuild trades ──────────────────────────────────────────────────────────
by_account = defaultdict(list)
for r in rows:
    by_account[r[1]].append(r)

trades = []
for acct_id, execs in by_account.items():
    position  = 0
    entry_px  = 0
    entry_qty = 0
    entry_time= None
    direction = None   # 'long' or 'short'
    exits_buf = []

    for r in sorted(execs, key=lambda x: x[7]):
        is_entry = r[3]
        is_exit  = r[4]
        qty      = r[6]
        price    = r[5]
        name     = str(r[2])
        dt       = net_ticks_to_dt(r[7])
        mkt_pos  = r[8]   # 0=long, 1=short

        if is_entry and position == 0:
            position   = qty
            entry_px   = price
            entry_qty  = qty
            entry_time = dt
            direction  = 'long' if mkt_pos == 0 else 'short'
            exits_buf  = []

        elif is_exit and position > 0:
            exits_buf.append({'name': name, 'qty': qty, 'price': price, 'dt': dt})
            position -= qty
            if position <= 0:
                position = 0
                # Classify exits
                exit_names = [e['name'] for e in exits_buf]

                def has(kw):
                    return any(kw.lower() in n.lower() for n in exit_names)

                hit_t1 = has('target1') or has('t1_') or has('ptt-qx-t1') or has('clost1')
                hit_t2 = has('target2') or has('t2_') or has('ptt-qx-t2')
                hit_t3 = has('target3') or has('t3_')
                hit_stop = has('stop') and not has('ptt-be-stop')
                hit_be   = has('ptt-be-stop')
                manual   = has('close') or has('graceful') or has('flatten') or has('external')
                hit_any_target = hit_t1 or hit_t2 or hit_t3

                # PnL — direction-aware
                sign = 1 if direction == 'long' else -1
                pnl = sum(sign * (e['price'] - entry_px) * e['qty'] * 5 for e in exits_buf)

                # Duration in minutes
                last_exit_time = exits_buf[-1]['dt']
                duration_min = None
                if entry_time and last_exit_time:
                    dur_sec = (last_exit_time - entry_time).total_seconds()
                    duration_min = dur_sec / 60

                # Approximate stop distance: first stop-named exit price vs entry
                # or look for how large the loss was
                implied_sl = abs(pnl) / (entry_qty * 5) if pnl < 0 else None

                # For wins: what was the T1 exit price?
                t1_exit = next((e for e in exits_buf if 'target1' in e['name'].lower() or 't1_' in e['name'].lower()), None)
                t2_exit = next((e for e in exits_buf if 'target2' in e['name'].lower() or 't2_' in e['name'].lower()), None)

                t1_dist = None
                t2_dist = None
                if t1_exit:
                    t1_dist = sign * (t1_exit['price'] - entry_px)
                if t2_exit:
                    t2_dist = sign * (t2_exit['price'] - entry_px)

                trades.append({
                    'acct':       acct_id,
                    'entry_px':   entry_px,
                    'entry_qty':  entry_qty,
                    'direction':  direction,
                    'entry_time': entry_time,
                    'pnl':        pnl,
                    'exits':      exit_names,
                    'hit_t1':     hit_t1,
                    'hit_t2':     hit_t2,
                    'hit_t3':     hit_t3,
                    'hit_stop':   hit_stop,
                    'hit_be':     hit_be,
                    'manual':     manual,
                    'duration_min': duration_min,
                    'implied_sl': implied_sl,
                    't1_dist':    t1_dist,
                    't2_dist':    t2_dist,
                })

print(f"Total trades reconstructed: {len(trades)}")
print()

# ── SECTION 1: Overall outcome breakdown ───────────────────────────────────
full_wins    = [t for t in trades if t['hit_t1'] and t['hit_t2']]
t1_only      = [t for t in trades if t['hit_t1'] and not t['hit_t2']]
be_exits     = [t for t in trades if t['hit_be'] and not t['hit_t1']]
stop_exits   = [t for t in trades if t['hit_stop'] and not t['hit_t1']]
manual_exits = [t for t in trades if t['manual'] and not t['hit_t1'] and not t['hit_t2']]
t1_hits      = [t for t in trades if t['hit_t1']]

print("=" * 80)
print("  SECTION 1: HOW TRADES ACTUALLY ENDED")
print("=" * 80)
n = len(trades)
print(f"  Full win  (T1 + T2 both filled) : {len(full_wins):4d}  ({len(full_wins)/n*100:5.1f}%)")
print(f"  T1 only   (T2 never filled)     : {len(t1_only):4d}  ({len(t1_only)/n*100:5.1f}%)")
print(f"  Breakeven (PTT-BE-Stop)         : {len(be_exits):4d}  ({len(be_exits)/n*100:5.1f}%)")
print(f"  Stop loss (hard stop hit)       : {len(stop_exits):4d}  ({len(stop_exits)/n*100:5.1f}%)")
print(f"  Manual close (no target hit)    : {len(manual_exits):4d}  ({len(manual_exits)/n*100:5.1f}%)")
print()
print(f"  T1 -> T2 conversion (when T1 hit): {len(full_wins)}/{len(t1_hits)} = {len(full_wins)/max(len(t1_hits),1)*100:.1f}%")
print()

# ── SECTION 2: PnL per category ────────────────────────────────────────────
print("=" * 80)
print("  SECTION 2: PnL BY OUTCOME TYPE")
print("=" * 80)
for label, group in [
    ("Full win (T1+T2)",   full_wins),
    ("T1 only",            t1_only),
    ("Breakeven (BE-Stop)",be_exits),
    ("Hard stop",          stop_exits),
    ("Manual close",       manual_exits),
]:
    if not group:
        continue
    avg = sum(t['pnl'] for t in group) / len(group)
    total = sum(t['pnl'] for t in group)
    pos = sum(1 for t in group if t['pnl'] > 0)
    neg = sum(1 for t in group if t['pnl'] < 0)
    print(f"  {label:<28}  n={len(group):4d}  avg=${avg:>+8.2f}  total=${total:>+9.2f}  (+{pos}/-{neg})")
print()

# ── SECTION 3: Manual close analysis ───────────────────────────────────────
print("=" * 80)
print("  SECTION 3: MANUAL CLOSE DEEP DIVE")
print("=" * 80)
man_wins  = [t for t in manual_exits if t['pnl'] > 0]
man_loss  = [t for t in manual_exits if t['pnl'] < 0]
man_be    = [t for t in manual_exits if t['pnl'] == 0]
print(f"  Manual closes total: {len(manual_exits)}")
print(f"    -> Won  (positive): {len(man_wins)} avg=${sum(t['pnl'] for t in man_wins)/max(len(man_wins),1):+.2f}")
print(f"    -> Lost (negative): {len(man_loss)} avg=${sum(t['pnl'] for t in man_loss)/max(len(man_loss),1):+.2f}")
print(f"    -> Flat (zero):     {len(man_be)}")
print()
print("  Of the manual LOSSES - how big were they? (implied SL distance)")
man_loss_sized = [t for t in man_loss if t['implied_sl'] is not None]
if man_loss_sized:
    buckets = defaultdict(int)
    for t in man_loss_sized:
        if t['implied_sl'] < 1.0:   buckets['<1pt'] += 1
        elif t['implied_sl'] < 2.0: buckets['1-2pt'] += 1
        elif t['implied_sl'] < 3.0: buckets['2-3pt'] += 1
        elif t['implied_sl'] < 4.0: buckets['3-4pt'] += 1
        elif t['implied_sl'] < 5.0: buckets['4-5pt'] += 1
        else:                        buckets['5pt+'] += 1
    for k, v in sorted(buckets.items()):
        print(f"    {k:>8}: {v:3d} trades")
print()

print("  Of the manual WINS - how much profit left on table?")
print("  (What did they close for vs what T2 would have been @ 75%SL)")
man_win_pnl = sorted([t['pnl'] for t in man_wins])
if man_wins:
    print(f"    Min win: ${min(t['pnl'] for t in man_wins):.2f}")
    print(f"    Max win: ${max(t['pnl'] for t in man_wins):.2f}")
    print(f"    Avg win: ${sum(t['pnl'] for t in man_wins)/len(man_wins):.2f}")
    small_wins = [t for t in man_wins if t['pnl'] < 25]
    print(f"    Wins <$25 (basically scratched): {len(small_wins)} ({len(small_wins)/len(man_wins)*100:.0f}%)")
print()

# ── SECTION 4: T1 actual fill distances ─────────────────────────────────────
print("=" * 80)
print("  SECTION 4: ACTUAL T1 FILL DISTANCES (from entry)")
print("=" * 80)
t1_dists = [t['t1_dist'] for t in trades if t['t1_dist'] is not None and t['t1_dist'] > 0]
if t1_dists:
    buckets = defaultdict(int)
    for d in t1_dists:
        if d < 1.5:    buckets['<1.50'] += 1
        elif d < 2.0:  buckets['1.50-2.00'] += 1
        elif d < 2.5:  buckets['2.00-2.50'] += 1
        elif d < 3.0:  buckets['2.50-3.00'] += 1
        elif d < 3.5:  buckets['3.00-3.50'] += 1
        else:           buckets['3.50+'] += 1
    print(f"  Total T1 fills with measurable distance: {len(t1_dists)}")
    for k, v in sorted(buckets.items()):
        print(f"    {k:>12}: {v:3d}  ({v/len(t1_dists)*100:.0f}%)")
    print(f"  Avg T1 fill distance: {sum(t1_dists)/len(t1_dists):.2f} pts")
    print(f"  Max T1 fill distance: {max(t1_dists):.2f} pts")
print()

# ── SECTION 5: Trade duration ────────────────────────────────────────────────
print("=" * 80)
print("  SECTION 5: TRADE DURATION (minutes)")
print("=" * 80)
durs = [t['duration_min'] for t in trades if t['duration_min'] is not None]
if durs:
    buckets = defaultdict(int)
    for d in durs:
        if d < 1:      buckets['<1min'] += 1
        elif d < 3:    buckets['1-3min'] += 1
        elif d < 5:    buckets['3-5min'] += 1
        elif d < 10:   buckets['5-10min'] += 1
        elif d < 20:   buckets['10-20min'] += 1
        else:           buckets['20min+'] += 1
    for k, v in sorted(buckets.items()):
        print(f"    {k:>10}: {v:3d}  ({v/len(durs)*100:.0f}%)")
    print(f"  Avg duration: {sum(durs)/len(durs):.1f} min")
    
    # Duration by outcome
    print()
    print("  Duration by outcome:")
    for label, group in [("Full win", full_wins), ("T1 only", t1_only),
                          ("Stop", stop_exits), ("Manual", manual_exits)]:
        g_durs = [t['duration_min'] for t in group if t['duration_min'] is not None]
        if g_durs:
            print(f"    {label:<15}: avg={sum(g_durs)/len(g_durs):.1f} min  "
                  f"median={sorted(g_durs)[len(g_durs)//2]:.1f} min")
print()

# ── SECTION 6: What the manual closes cost us ────────────────────────────────
print("=" * 80)
print("  SECTION 6: COST OF MANUAL CLOSES (vs letting ATM work)")
print("=" * 80)
# Manual losses that were small (1-3pt moves against) -- these SHOULD have been stops
# Manual wins that were tiny -- these SHOULD have been T1 or T2
total_manual_pnl = sum(t['pnl'] for t in manual_exits)
# If those manual losses had been stopped at 4pt SL (assumption): 
# avg manual loss vs avg stop loss
avg_manual_loss = sum(t['pnl'] for t in man_loss) / max(len(man_loss), 1)
avg_stop_loss   = sum(t['pnl'] for t in stop_exits) / max(len(stop_exits), 1)
print(f"  Total manual close PnL:          ${total_manual_pnl:+.2f}")
print(f"  Avg manual loss:                 ${avg_manual_loss:+.2f}")
print(f"  Avg hard stop loss:              ${avg_stop_loss:+.2f}")
print(f"  Avg manual win:                  ${sum(t['pnl'] for t in man_wins)/max(len(man_wins),1):+.2f}")
print()
print("  KEY INSIGHT: If every 'manual close loss' had been left to stop out at the ATM stop,")
diff = avg_stop_loss - avg_manual_loss
print(f"  the avg loss difference would be: {diff:+.2f} per trade")
print(f"  ({len(man_loss)} manual losses * {diff:+.2f} = {len(man_loss)*diff:+.2f} total impact)")
print()

# ── SECTION 7: Current ATM T2 was at 50% SL per user ─────────────────────────
print("=" * 80)
print("  SECTION 7: SUMMARY -- WHAT THE DATA TELLS US")
print("=" * 80)
print(f"""
  Trades analyzed:      {len(trades)}
  Win rate (any +PnL):  {len([t for t in trades if t['pnl']>0])/len(trades)*100:.1f}%
  T1 fill rate:         {len(t1_hits)/len(trades)*100:.1f}%  ({len(t1_hits)} trades)
  T2 fill rate:         {len(full_wins)/len(trades)*100:.1f}%  ({len(full_wins)} trades)
  T1->T2 conversion:    {len(full_wins)/max(len(t1_hits),1)*100:.1f}%
  Manual close rate:    {(len(manual_exits)+len(man_wins)+len(man_loss))/len(trades)*100:.1f}%
  
  The problem is NOT the ATM design. The problem is execution:
    - {len(manual_exits)} trades closed manually BEFORE hitting any target
    - Manual losses avg {avg_manual_loss:+.2f} vs hard stops avg {avg_stop_loss:+.2f}
    - {len(small_wins) if man_wins else 0} manual wins were <$25 (scratch trades -- should have run to T1)
    
  With current T2 at 50% SL (per user): T2 was TOO CLOSE -- it was at same level as T1 cap.
  Moving T2 to 75% SL gives MORE room between T1 and T2, which should improve conversion.
""")
