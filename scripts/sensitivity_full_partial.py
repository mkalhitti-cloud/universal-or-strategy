"""
Scenario sensitivity analysis — vary the Full Win vs Partial (T1+BE) split
while keeping hard stop and BE-only fixed.
Shows E/trade for each SL across different full/partial ratios.
"""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

# Final approved config
CONFIGS = [
    # sl, t1, t2, qty_t1, qty_t2
    (4, 2.00, 3.00, 10, 10),
    (5, 2.50, 3.75,  8,  8),
    (6, 3.00, 4.50,  6,  7),
    (7, 3.50, 5.25,  5,  6),
    (8, 4.00, 6.00,  5,  5),
]
FEE_PER_CONTRACT = 0.57
BE_TRIGGER = 2.00
USAGE_WEIGHTS = {4: 0.45, 5: 0.30, 6: 0.15, 7: 0.07, 8: 0.03}

# Fixed probabilities (the "envelope" that doesn't change)
P_BE_ONLY = 0.15   # hits 2pt zone, whole pos exits BE
P_STOP    = 0.25   # hard stop before BE trigger
# Remaining 60% is split between full win (A) and T1+BE partial (B)
REMAINING = 1.0 - P_BE_ONLY - P_STOP   # = 0.60

# Splits to test: (full_win_pct_of_remaining, label)
SPLITS = [
    (1.00, "100% full / 0% partial  (T2 always fills after T1)"),
    (0.80, " 80% full / 20% partial"),
    (0.60, " 60% full / 40% partial"),
    (0.50, " 50% full / 50% partial  (baseline target)"),
    (0.40, " 40% full / 60% partial"),
    (0.20, " 20% full / 80% partial  <-- your question"),
    (0.10, " 10% full / 90% partial"),
    (0.00, "  0% full / 100% partial (T2 NEVER fills after T1)"),
]

# Pre-compute PnL outcomes per config
def compute(sl, t1, t2, qty_t1, qty_t2):
    fees = FEE_PER_CONTRACT * (qty_t1 + qty_t2)
    total_qty = qty_t1 + qty_t2
    pnl_full    = (t1 * qty_t1 * 5) + (t2 * qty_t2 * 5) - fees
    pnl_t1_be   = (t1 * qty_t1 * 5) + 0 - fees          # runner exits at entry
    pnl_be_only = (BE_TRIGGER * total_qty * 5) - fees    # whole pos at +2pts
    pnl_stop    = -(sl * total_qty * 5) - fees
    return pnl_full, pnl_t1_be, pnl_be_only, pnl_stop

outcomes = {}
for (sl, t1, t2, qt1, qt2) in CONFIGS:
    outcomes[sl] = compute(sl, t1, t2, qt1, qt2)

SEP = "=" * 120
DIV = "-" * 120

print(SEP)
print("  SENSITIVITY: E/TRADE vs FULL-WIN / PARTIAL SPLIT")
print("  Fixed: P(BE-only)=15%  P(Hard Stop)=25%  |  Remaining 60% split between Full Win and T1+BE")
print("  Config: T1=50%SL  T2=75%SL  BE@2pts  Qty=50/50 split")
print(SEP)
print()

# Table 1: E per SL per split
print(f"  {'Split':46} | {'SL4':>8} {'SL5':>8} {'SL6':>8} {'SL7':>8} {'SL8':>8} | {'Wtd E':>8}")
print(f"  {'-'*46}   {'-'*8} {'-'*8} {'-'*8} {'-'*8} {'-'*8}   {'-'*8}")

for (full_pct, label) in SPLITS:
    p_full  = REMAINING * full_pct
    p_t1_be = REMAINING * (1.0 - full_pct)
    
    row = {}
    for (sl, t1, t2, qt1, qt2) in CONFIGS:
        pf, pt, pb, ps = outcomes[sl]
        e = p_full*pf + p_t1_be*pt + P_BE_ONLY*pb + P_STOP*ps
        row[sl] = e
    
    wtd = sum(USAGE_WEIGHTS[sl] * row[sl] for sl in row)
    marker = " <--" if abs(full_pct - 0.20) < 0.001 else ""
    print(f"  {label:<46} | "
          f"{row[4]:>+8.2f} {row[5]:>+8.2f} {row[6]:>+8.2f} "
          f"{row[7]:>+8.2f} {row[8]:>+8.2f} | {wtd:>+8.2f}{marker}")

print()
print(SEP)
print("  DEEP DIVE: 20% FULL / 80% PARTIAL SCENARIO")
print(SEP)

p_full_20  = REMAINING * 0.20   # = 0.12
p_t1_be_80 = REMAINING * 0.80   # = 0.48

print(f"  Probabilities at 20/80 split:")
print(f"    A: Full win (T1+T2)      = {p_full_20:.0%}  (was 35% target)")
print(f"    B: T1 + BE stop          = {p_t1_be_80:.0%}  (was 25% target)")
print(f"    C: BE only (whole pos)   = {P_BE_ONLY:.0%}")
print(f"    D: Hard stop             = {P_STOP:.0%}")
print()
print(f"  {'SL':<6} {'T1':>6} {'T2':>6} {'Qty':>7} | "
      f"{'FullWin':>9} {'T1+BE':>9} {'BEonly':>9} {'Stop':>9} | "
      f"{'E/trade':>9} | {'Need T2%':>9}")
print(f"  {'-'*6} {'-'*6} {'-'*6} {'-'*7}   "
      f"{'-'*9} {'-'*9} {'-'*9} {'-'*9}   {'-'*9}   {'-'*9}")

system_e_20 = 0
for (sl, t1, t2, qt1, qt2) in CONFIGS:
    pf, pt, pb, ps = outcomes[sl]
    e = p_full_20*pf + p_t1_be_80*pt + P_BE_ONLY*pb + P_STOP*ps
    
    # What % of T1 hits need to become T2 fills to break even?
    # E = x*pf + (0.60-x)*pt + P_BE_ONLY*pb + P_STOP*ps = 0
    # x*(pf-pt) = -(0.60*pt + P_BE_ONLY*pb + P_STOP*ps)
    # x = -(0.60*pt + P_BE_ONLY*pb + P_STOP*ps) / (pf-pt)
    numer = -(REMAINING*pt + P_BE_ONLY*pb + P_STOP*ps)
    denom = pf - pt
    be_x = numer / denom if denom != 0 else float('nan')
    # be_x is fraction of the 60% pool that needs to be full wins
    # as % of T1 hits (T1 hits = full + partial = 60% of trades):
    be_pct_of_t1 = be_x / REMAINING if REMAINING != 0 else float('nan')
    
    system_e_20 += USAGE_WEIGHTS[sl] * e
    print(f"  SL{sl}    {t1:>6.2f} {t2:>6.2f} {qt1}+{qt2:>1}     | "
          f"{pf:>+9.2f} {pt:>+9.2f} {pb:>+9.2f} {ps:>+9.2f} | "
          f"{e:>+9.2f} | {be_pct_of_t1:>8.1%}")

print()
print(f"  System weighted E at 20% full / 80% partial: {system_e_20:+.2f}")
print()

# Table 2: what full-win % of T1 hits is needed to break even per SL
print(SEP)
print("  BREAKEVEN TABLE: % of T1 hits that must reach T2 to break even")
print("  (i.e. after T1 fills, what % of the time must T2 also fill?)")
print(SEP)
print(f"  {'SL':<6} {'T1':>6} {'T2':>6} | {'BE% of T1 hits':>16} | {'Dollar gap T1 vs full':>22}")
print(f"  {'-'*6} {'-'*6} {'-'*6}   {'-'*16}   {'-'*22}")
for (sl, t1, t2, qt1, qt2) in CONFIGS:
    pf, pt, pb, ps = outcomes[sl]
    numer = -(REMAINING*pt + P_BE_ONLY*pb + P_STOP*ps)
    denom = pf - pt
    be_x = numer / denom if denom != 0 else float('nan')
    be_pct = be_x / REMAINING
    t2_add = pf - pt   # extra dollars from T2 filling vs just T1+BE
    print(f"  SL{sl}    {t1:>6.2f} {t2:>6.2f} | {be_pct:>15.1%} | T2 adds ${t2_add:>+8.2f} when it fills")

print()
print(SEP)
print("  KEY QUESTION: Is positive E still achievable at 20% full / 80% partial?")
print(SEP)
for (sl, t1, t2, qt1, qt2) in CONFIGS:
    pf, pt, pb, ps = outcomes[sl]
    e_20 = p_full_20*pf + p_t1_be_80*pt + P_BE_ONLY*pb + P_STOP*ps
    e_50 = (REMAINING*0.50)*pf + (REMAINING*0.50)*pt + P_BE_ONLY*pb + P_STOP*ps
    sign = "POSITIVE" if e_20 > 0 else "NEGATIVE"
    print(f"  SL{sl}: E at 20/80 = {e_20:>+8.2f}  ({sign})  |  E at 50/50 = {e_50:>+8.2f}  |  "
          f"Diff = {e_20-e_50:>+7.2f}")
