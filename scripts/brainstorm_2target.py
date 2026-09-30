"""
MES ATM Brainstorm - 2-target design with 75pct T2 cap
"""
import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

SL_LIST = [4, 5, 6, 7, 8]
T1_OPTIONS = [2.00, 2.25, 2.50, 2.75, 3.00]
T2_OPTIONS = [3.00, 3.25, 3.50, 3.75, 4.00]
FEE_PER_CONTRACT = 0.57
P_FULL = 0.45
P_PARTIAL = 0.30
P_STOP = 0.25
MAX_RISK = 400
T1_CAP_PCT = 0.50   # T1 max = 50% of SL
T2_CAP_PCT = 0.75   # T2 max = 75% of SL
USAGE_WEIGHTS = {4: 0.45, 5: 0.30, 6: 0.15, 7: 0.07, 8: 0.03}

results = []

for sl in SL_LIST:
    total_qty = min(20, int(MAX_RISK / (sl * 5)))
    qty_t1 = total_qty // 2
    qty_t2 = total_qty - qty_t1
    t1_cap = sl * T1_CAP_PCT
    t2_cap = sl * T2_CAP_PCT

    for t1 in T1_OPTIONS:
        if t1 > t1_cap:
            continue          # T1 exceeds 50% of SL — skip
        for t2_raw in T2_OPTIONS:
            t2 = min(t2_raw, t2_cap)
            if t2 <= t1:
                continue
            fees = FEE_PER_CONTRACT * total_qty
            full_win  = (t1 * qty_t1 * 5) + (t2 * qty_t2 * 5) - fees
            partial   = (t1 * qty_t1 * 5) - fees
            stop_val  = -(sl * total_qty * 5) - fees
            e_trade   = P_FULL * full_win + P_PARTIAL * partial + P_STOP * stop_val
            results.append({
                'sl': sl, 'total_qty': total_qty,
                'qty_t1': qty_t1, 'qty_t2': qty_t2,
                't1': t1, 't2': t2, 't2_raw': t2_raw,
                't2_capped': t2 < t2_raw,
                't1_pct_sl': (t1/sl)*100,
                't2_pct_sl': (t2/sl)*100,
                'risk': sl*total_qty*5 + fees,
                'full_win': full_win, 'partial': partial,
                'stop': stop_val, 'e_trade': e_trade,
            })

SEP = "=" * 130
DIV = "-" * 130

print(SEP)
print("  MES $400 ATM BRAINSTORM -- 2-TARGET DESIGN  (T1 max=50%SL  |  T2 max=75%SL)")
print(SEP)
print(f"  Probabilities: Full Win={P_FULL:.0%}  Partial={P_PARTIAL:.0%}  Stop={P_STOP:.0%}")
print(f"  Fee: ${FEE_PER_CONTRACT}/contract RT | Contract split: 50/50")
print()

best_per_sl = {}

for sl in SL_LIST:
    sl_rows = [r for r in results if r['sl'] == sl]
    total_qty = sl_rows[0]['total_qty']
    risk_approx = sl * total_qty * 5

    print(DIV)
    print(f"  SL{sl} | {total_qty} contracts ({total_qty//2} T1 + {total_qty - total_qty//2} T2) | Max risk ~${risk_approx:,}  "
          f"(T1 max={sl*T1_CAP_PCT:.2f}  T2 max={sl*T2_CAP_PCT:.2f})")
    print(DIV)
    print(f"  {'T1':>5} {'T1%SL':>6} | {'T2':>5} {'T2%SL':>6} {'Capped':>7} | {'FullWin':>9} {'Partial':>9} {'Stop':>9} | {'E/trade':>9} | Grade")
    print(f"  {'-'*5} {'-'*6}   {'-'*5} {'-'*6} {'-'*7}   {'-'*9} {'-'*9} {'-'*9}   {'-'*9}  ")

    best_e = max(r['e_trade'] for r in sl_rows)
    best_per_sl[sl] = max(sl_rows, key=lambda r: r['e_trade'])

    for r in sorted(sl_rows, key=lambda x: (x['t1'], x['t2_raw'])):
        cap_note = "YES" if r['t2_capped'] else "   "
        if r['e_trade'] >= best_e * 0.95:
            grade = "*** BEST"
        elif r['e_trade'] >= best_e * 0.80:
            grade = "**      "
        elif r['e_trade'] > 0:
            grade = "*       "
        else:
            grade = "        "
        print(f"  {r['t1']:>5.2f} {r['t1_pct_sl']:>5.0f}%  | "
              f"{r['t2']:>5.2f} {r['t2_pct_sl']:>5.0f}% {cap_note:>7} | "
              f"{r['full_win']:>+9.2f} {r['partial']:>+9.2f} {r['stop']:>+9.2f} | "
              f"{r['e_trade']:>+9.2f} | {grade}")
    print()

# System-level weighted E matrix
print(SEP)
print("  SYSTEM-LEVEL WEIGHTED E/TRADE MATRIX (usage: SL4=45%, SL5=30%, SL6=15%, SL7=7%, SL8=3%)")
print(SEP)
print(f"  {'T1':>5}  {'T2 req':>6} |  {'SL4':>8}  {'SL5':>8}  {'SL6':>8}  {'SL7':>8}  {'SL8':>8}  | {'Wtd E/tr':>9} | Notes")
print(f"  {'-'*5}  {'-'*6}    {'-'*8}  {'-'*8}  {'-'*8}  {'-'*8}  {'-'*8}    {'-'*9}  ")

highlight_rows = []

for t1 in T1_OPTIONS:
    for t2_raw in T2_OPTIONS:
        row_by_sl = {}
        for r in results:
            if abs(r['t1'] - t1) < 0.001 and abs(r['t2_raw'] - t2_raw) < 0.001:
                row_by_sl[r['sl']] = r
        if not row_by_sl:
            continue

        weighted_e = sum(
            USAGE_WEIGHTS.get(sl, 0) * row_by_sl[sl]['e_trade']
            for sl in SL_LIST if sl in row_by_sl
        )

        cells = ""
        for sl in SL_LIST:
            if sl in row_by_sl:
                cells += f"  {row_by_sl[sl]['e_trade']:>+8.2f}"
            else:
                cells += f"  {'  skip':>8}"

        notes = ""
        if abs(t1 - 2.50) < 0.001 and abs(t2_raw - 3.75) < 0.001:
            notes = "<-- balanced mid"
        if abs(t1 - 3.00) < 0.001 and abs(t2_raw - 4.00) < 0.001:
            notes = "<-- max (but T2 capped for SL4/5)"
        if abs(t1 - 2.25) < 0.001 and abs(t2_raw - 3.00) < 0.001:
            notes = "<-- conservative"
        if abs(t1 - 2.75) < 0.001 and abs(t2_raw - 3.75) < 0.001:
            notes = "<-- David-style"

        highlight_rows.append((t1, t2_raw, weighted_e))
        print(f"  {t1:>5.2f}  {t2_raw:>6.2f} |{cells}  | {weighted_e:>+9.2f} | {notes}")

print()

# Best system config
print(SEP)
print("  BEST CONFIG PER SL (highest E/trade with 75pct T2 cap)")
print(SEP)
system_e_best = 0
for sl in SL_LIST:
    b = best_per_sl[sl]
    cap_note = f"  [T2 capped: {b['t2_raw']:.2f} -> {b['t2']:.2f}]" if b['t2_capped'] else ""
    print(f"  SL{sl}:  T1={b['t1']:.2f} ({b['t1_pct_sl']:.0f}%SL)  "
          f"T2={b['t2']:.2f} ({b['t2_pct_sl']:.0f}%SL)  "
          f"Qty={b['qty_t1']}+{b['qty_t2']}  "
          f"E/trade={b['e_trade']:+.2f}{cap_note}")
    system_e_best += USAGE_WEIGHTS[sl] * b['e_trade']

print()
print(f"  System E/trade (best per SL, usage-weighted) : {system_e_best:+.2f}")
print(f"  Current system E/trade (from real NT8 data)  : -20.99")
print(f"  Improvement per trade                        : {system_e_best - (-20.99):+.2f}")
print()

# Breakeven win rate for SL4 best
b4 = best_per_sl[4]
avg_win_4 = (P_FULL * b4['full_win'] + P_PARTIAL * b4['partial']) / (P_FULL + P_PARTIAL)
avg_loss_4 = abs(b4['stop'])
be_wr = avg_loss_4 / (avg_win_4 + avg_loss_4)
print(f"  SL4 best config breakeven analysis:")
print(f"    Blended avg win  = ${avg_win_4:.2f}")
print(f"    Avg loss         = ${avg_loss_4:.2f}")
print(f"    Breakeven WR     = {be_wr:.1%}  (your actual WR = 45.3% from NT8 data)")
print()

# Best overall system weighted E
top5 = sorted(highlight_rows, key=lambda x: -x[2])[:5]
print("  TOP 5 SINGLE T1/T2 COMBOS BY SYSTEM WEIGHTED E:")
for i, (t1, t2r, we) in enumerate(top5, 1):
    # show what T2 actually becomes per SL
    t2_actual = {sl: min(t2r, sl*0.75) for sl in SL_LIST}
    t2_str = "  ".join(f"SL{sl}:{t2_actual[sl]:.2f}" for sl in SL_LIST)
    print(f"  #{i}: T1={t1:.2f}  T2_req={t2r:.2f}  Actual T2: [{t2_str}]  Wtd E={we:+.2f}")
