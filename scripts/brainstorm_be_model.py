"""
MES ATM Expectancy Model -- BE at 2pts across the board
Key insight: BE stop moves to entry+2pts after T1 fills (or just at 2pts)
So the real trade is:
  - Entry fires
  - Price moves to BE zone (2pts) = stop goes to breakeven
  - T1 fills = coffee money, confirms momentum, remainder is now risk-free
  - T2 fills = the REAL win, this is what drives expectancy
  - If stopped at BE after T1 = tiny net win (T1 profit only, no loss on remainder)

New scenario model (4 outcomes):
  A. Full win:  T1 + T2 both fill                     P=0.35 (target)
  B. T1 + BE:   T1 fills, remainder stopped at entry  P=0.25 (current ~12%, target 25%)
  C. BE stop:   Price hits 2pt zone, stopped at BE     P=0.15
  D. Stop loss: Stopped before 2pts                    P=0.25

PnL per scenario:
  A. full_win   = T1*qty_t1*5 + T2*qty_t2*5 - fees
  B. t1_be      = T1*qty_t1*5 + 0*qty_t2   - fees   (remainder exits at entry = $0 on that half)
  C. be_stop    = +2.00*total_qty*5         - fees   (whole position stopped at +2pts)
  D. stop_loss  = -SL*total_qty*5           - fees

NOTE: BE at 2pts means even scenario D (stop) only fires BEFORE 2pts are reached.
So scenario D is only trades that reverse hard before the 2pt BE trigger.
"""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

SL_LIST = [4, 5, 6, 7, 8]
# T1: 2.00 to 4.00 in 0.25pt steps — cap filter (50%SL) trims per SL
T1_OPTIONS = [2.00, 2.25, 2.50, 2.75, 3.00, 3.25, 3.50, 3.75, 4.00]
# T2: 3.00 to 6.00 in 0.25pt steps — cap filter (75%SL) trims per SL
T2_OPTIONS = [3.00, 3.25, 3.50, 3.75, 4.00, 4.25, 4.50, 4.75, 5.00, 5.25, 5.50, 5.75, 6.00]
FEE_PER_CONTRACT = 0.57
BE_TRIGGER = 2.00   # BE stop moves to entry after price reaches this

T1_CAP_PCT = 0.50
T2_CAP_PCT = 0.75
MIN_GAP    = 1.00   # minimum T2 - T1

MAX_RISK = 400
USAGE_WEIGHTS = {4: 0.45, 5: 0.30, 6: 0.15, 7: 0.07, 8: 0.03}

# Scenario probabilities -- using realistic targets
# A: full win (T1+T2)     -- what we're designing for
# B: T1 + BE stop         -- T1 fills, remainder stopped at entry
# C: BE stop only         -- price reaches 2pts then reverses, whole position BE
# D: hard stop            -- price reverses before 2pt BE trigger
P_FULL    = 0.35   # realistic target with proper T2 distance
P_T1_BE   = 0.25   # T1 fills, runner stopped at BE
P_BE_ONLY = 0.15   # hits 2pt zone but T1 doesn't fill, whole pos exits at +2
P_STOP    = 0.25   # hard stop before BE trigger

# Also run with current real-data probabilities for comparison
P_FULL_REAL    = 0.029  # actual from data
P_T1_BE_REAL   = 0.210  # T1 only from data (most ended at loss because T2 at 50%SL)
P_BE_ONLY_REAL = 0.024  # actual BE stops
P_STOP_REAL    = 0.119  # actual hard stops
P_MANUAL_REAL  = 0.547  # manual closes -- modeled as small loss for comparison

results = []

for sl in SL_LIST:
    total_qty = min(20, int(MAX_RISK / (sl * 5)))
    qty_t1 = total_qty // 2
    qty_t2 = total_qty - qty_t1
    t1_cap = sl * T1_CAP_PCT
    t2_cap = sl * T2_CAP_PCT

    for t1 in T1_OPTIONS:
        if t1 > t1_cap:
            continue
        # T1 must be >= BE trigger to make sense (otherwise T1 is below BE)
        # T1 at 50%SL on SL4 = 2.00 = exactly at BE trigger. That's fine.
        for t2_raw in T2_OPTIONS:
            t2 = min(t2_raw, t2_cap)
            if t2 <= t1:
                continue
            if (t2 - t1) < MIN_GAP:
                continue

            fees = FEE_PER_CONTRACT * total_qty

            # Scenario A: T1 + T2 both fill
            pnl_full = (t1 * qty_t1 * 5) + (t2 * qty_t2 * 5) - fees

            # Scenario B: T1 fills, remainder stopped at entry (BE)
            # qty_t1 exits at T1, qty_t2 exits at entry price = $0
            pnl_t1_be = (t1 * qty_t1 * 5) + 0 - fees

            # Scenario C: BE stop -- whole position exits at entry+2pts
            pnl_be_only = (BE_TRIGGER * total_qty * 5) - fees

            # Scenario D: Hard stop
            pnl_stop = -(sl * total_qty * 5) - fees

            # Expected value -- TARGET probabilities
            e_target = (P_FULL    * pnl_full +
                        P_T1_BE   * pnl_t1_be +
                        P_BE_ONLY * pnl_be_only +
                        P_STOP    * pnl_stop)

            # Expected value -- REAL current probabilities (for comparison)
            # Manual closes averaged -$14.65, model as that
            e_real = (P_FULL_REAL  * pnl_full +
                      P_T1_BE_REAL * pnl_t1_be +
                      P_BE_ONLY_REAL * pnl_be_only +
                      P_STOP_REAL  * pnl_stop +
                      P_MANUAL_REAL * -14.65)

            results.append({
                'sl': sl, 'total_qty': total_qty,
                'qty_t1': qty_t1, 'qty_t2': qty_t2,
                't1': t1, 't2': t2, 't2_raw': t2_raw,
                't2_capped': t2 < t2_raw,
                't1_pct_sl': (t1/sl)*100,
                't2_pct_sl': (t2/sl)*100,
                'gap': t2 - t1,
                'pnl_full': pnl_full,
                'pnl_t1_be': pnl_t1_be,
                'pnl_be_only': pnl_be_only,
                'pnl_stop': pnl_stop,
                'e_target': e_target,
                'e_real': e_real,
            })

SEP = "=" * 145
DIV = "-" * 145

print(SEP)
print("  MES $400 -- BE AT 2pts ACROSS THE BOARD  |  T1=coffee money  |  T2=the real win")
print(SEP)
print(f"  BE trigger: {BE_TRIGGER}pts (stop moves to entry after price reaches 2pts)")
print(f"  T1 cap: {T1_CAP_PCT*100:.0f}%SL  |  T2 cap: {T2_CAP_PCT*100:.0f}%SL  |  Min T1->T2 gap: {MIN_GAP}pt")
print()
print("  SCENARIO PROBABILITIES:")
print(f"  {'':30} {'TARGET':>10} {'ACTUAL(now)':>12}")
print(f"  {'A: Full win (T1+T2 both fill)':30} {P_FULL:>10.1%} {P_FULL_REAL:>12.1%}")
print(f"  {'B: T1 fills, runner stopped BE':30} {P_T1_BE:>10.1%} {P_T1_BE_REAL:>12.1%}")
print(f"  {'C: BE stop (whole pos, no T1)':30} {P_BE_ONLY:>10.1%} {P_BE_ONLY_REAL:>12.1%}")
print(f"  {'D: Hard stop (before BE)':30} {P_STOP:>10.1%} {P_STOP_REAL:>12.1%}")
print(f"  {'E: Manual close (target model)':30} {'0.0%':>10} {P_MANUAL_REAL:>12.1%}")
print()

best_per_sl = {}

for sl in SL_LIST:
    sl_rows = [r for r in results if r['sl'] == sl]
    if not sl_rows:
        continue
    total_qty = sl_rows[0]['total_qty']
    risk_approx = sl * total_qty * 5

    print(DIV)
    print(f"  SL{sl} | {total_qty} contracts ({total_qty//2}T1 + {total_qty-total_qty//2}T2) | "
          f"Max risk ~${risk_approx:,}  |  T1 max={sl*T1_CAP_PCT:.2f}pt  T2 max={sl*T2_CAP_PCT:.2f}pt")
    print(DIV)
    print(f"  {'T1':>5} {'T1%':>5} | {'T2':>5} {'T2%':>5} {'Gap':>5} | "
          f"{'A:FullWin':>10} {'B:T1+BE':>10} {'C:BEonly':>10} {'D:Stop':>10} | "
          f"{'E(target)':>10} {'E(actual)':>10}")
    print(f"  {'-'*5} {'-'*5}   {'-'*5} {'-'*5} {'-'*5}   "
          f"{'-'*10} {'-'*10} {'-'*10} {'-'*10}   {'-'*10} {'-'*10}")

    best_e = max(r['e_target'] for r in sl_rows)
    best_per_sl[sl] = max(sl_rows, key=lambda r: r['e_target'])

    for r in sorted(sl_rows, key=lambda x: (x['t1'], x['t2_raw'])):
        cap = "*" if r['t2_capped'] else " "
        mark = " <-- BEST" if r['e_target'] >= best_e * 0.99 else ""
        print(f"  {r['t1']:>5.2f} {r['t1_pct_sl']:>4.0f}%  | "
              f"{r['t2']:>5.2f} {r['t2_pct_sl']:>4.0f}%{cap} {r['gap']:>5.2f} | "
              f"{r['pnl_full']:>+10.2f} {r['pnl_t1_be']:>+10.2f} "
              f"{r['pnl_be_only']:>+10.2f} {r['pnl_stop']:>+10.2f} | "
              f"{r['e_target']:>+10.2f} {r['e_real']:>+10.2f}{mark}")
    print()

# System-level weighted E matrix
print(SEP)
print("  SYSTEM WEIGHTED E/TRADE MATRIX  (SL4=45% SL5=30% SL6=15% SL7=7% SL8=3%)")
print(SEP)
print(f"  {'T1':>5}  {'T2req':>5} | {'SL4':>9} {'SL5':>9} {'SL6':>9} {'SL7':>9} {'SL8':>9} | "
      f"{'Wtd E(tgt)':>11} {'Wtd E(now)':>11}")
print(f"  {'-'*5}  {'-'*5}   {'-'*9} {'-'*9} {'-'*9} {'-'*9} {'-'*9}   {'-'*11} {'-'*11}")

highlight = []
for t1 in T1_OPTIONS:
    for t2_raw in T2_OPTIONS:
        row_by_sl = {r['sl']: r for r in results
                     if abs(r['t1']-t1)<0.001 and abs(r['t2_raw']-t2_raw)<0.001}
        if not row_by_sl:
            continue
        wtd_tgt = sum(USAGE_WEIGHTS.get(sl,0)*row_by_sl[sl]['e_target']
                      for sl in SL_LIST if sl in row_by_sl)
        wtd_now = sum(USAGE_WEIGHTS.get(sl,0)*row_by_sl[sl]['e_real']
                      for sl in SL_LIST if sl in row_by_sl)
        cells = ""
        for sl in SL_LIST:
            if sl in row_by_sl:
                cells += f" {row_by_sl[sl]['e_target']:>+9.2f}"
            else:
                cells += f" {'  skip':>9}"
        highlight.append((t1, t2_raw, wtd_tgt, wtd_now))
        print(f"  {t1:>5.2f}  {t2_raw:>5.2f} |{cells} | {wtd_tgt:>+11.2f} {wtd_now:>+11.2f}")

print()
print(SEP)
print("  FINAL RECOMMENDED CONFIG (best E per SL, both target and actual scenarios)")
print(SEP)
system_tgt = 0
system_now = 0
print(f"  {'SL':>4} {'T1':>6} {'T1%':>5} {'T2':>6} {'T2%':>5} {'Gap':>5} "
      f"{'Qty':>8} {'FullWin':>9} {'T1+BE':>9} {'BEonly':>9} {'Stop':>9} "
      f"{'E(tgt)':>9} {'E(now)':>9}")
print(f"  {'-'*4} {'-'*6} {'-'*5} {'-'*6} {'-'*5} {'-'*5} "
      f"{'-'*8} {'-'*9} {'-'*9} {'-'*9} {'-'*9} {'-'*9} {'-'*9}")
for sl in SL_LIST:
    b = best_per_sl[sl]
    print(f"  SL{sl}  {b['t1']:>6.2f} {b['t1_pct_sl']:>4.0f}%  "
          f"{b['t2']:>6.2f} {b['t2_pct_sl']:>4.0f}%  {b['gap']:>5.2f}  "
          f"{b['qty_t1']}+{b['qty_t2']:>1}     "
          f"{b['pnl_full']:>+9.2f} {b['pnl_t1_be']:>+9.2f} "
          f"{b['pnl_be_only']:>+9.2f} {b['pnl_stop']:>+9.2f} "
          f"{b['e_target']:>+9.2f} {b['e_real']:>+9.2f}")
    system_tgt += USAGE_WEIGHTS[sl] * b['e_target']
    system_now += USAGE_WEIGHTS[sl] * b['e_real']

print()
print(f"  System E/trade (TARGET scenario, use-weighted):  {system_tgt:+.2f}")
print(f"  System E/trade (ACTUAL current, use-weighted):   {system_now:+.2f}")
print(f"  Historical E/trade (from real NT8 data):         -20.99")
print()
print("  BREAKEVEN ANALYSIS per SL:")
for sl in SL_LIST:
    b = best_per_sl[sl]
    # Full win needed to overcome stop: solve for P_FULL where E=0
    # E = P_FULL*pnl_full + P_T1_BE*pnl_t1_be + P_BE_ONLY*pnl_be_only + P_STOP*pnl_stop = 0
    # Assume P_STOP=0.25, P_BE_ONLY=0.15 fixed, P_T1_BE = 1-P_FULL-0.25-0.15
    # E = P_FULL*F + (0.60-P_FULL)*T + 0.15*B + 0.25*S = 0
    F = b['pnl_full']
    T = b['pnl_t1_be']
    B = b['pnl_be_only']
    S = b['pnl_stop']
    # 0 = P_FULL*(F-T) + 0.60*T + 0.15*B + 0.25*S
    # P_FULL = -(0.60*T + 0.15*B + 0.25*S) / (F-T)
    numerator   = -(0.60*T + 0.15*B + 0.25*S)
    denominator = F - T
    be_p_full = numerator / denominator if denominator != 0 else float('nan')
    print(f"  SL{sl}: need {be_p_full:.1%} full-win rate (T1+T2 both) to break even  "
          f"[currently {P_FULL_REAL:.1%}]")
