"""
Updated expectancy model incorporating:
1. BE Abort button  -- when trade goes against you >2pts, click BE abort,
                       exit at first BE opportunity instead of full stop loss.
                       Converts some D (hard stop -$411) into D_reduced (partial loss ~-$100 to -$150)
2. Quick Exit (T1=1pt, T2=2pt) -- when trade looks like a dud, switch ATM mid-trade.
                       Converts some B (T1+BE = coffee money) into QuickExit (~+$50-80)
                       and avoids the T2 miss entirely.

The key insight: these buttons SKEW THE LOSS DISTRIBUTION.
  - Hard stops become smaller losses (BE abort catches them earlier)
  - Dud trades exit for small wins instead of waiting and getting stopped

New 5-outcome model:
  A: Full win     -- T1+T2 both fill on original ATM           P=0.35
  B: T1+BE        -- T1 fills, runner stopped at entry          P=0.20
  C: BE only      -- whole pos exits at +2pts                   P=0.15
  D: Hard stop    -- full SL hit, no intervention               P=0.10  (was 0.25, reduced by buttons)
  E: BE abort     -- trade went against, exited near BE on way back  P=0.10  (avg loss ~-$100)
  F: Quick exit   -- switched to 1pt/2pt targets, small win     P=0.10  (avg ~+$50)

Note: D+E+F = 0.30 (same as old D=0.25 + some from B that were dud trades)
The total must sum to 1.0: 0.35+0.20+0.15+0.10+0.10+0.10 = 1.00
"""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

CONFIGS = [
    (4, 2.00, 3.00, 10, 10),
    (5, 2.50, 3.75,  8,  8),
    (6, 3.00, 4.50,  6,  7),
    (7, 3.50, 5.25,  5,  6),
    (8, 4.00, 6.00,  5,  5),
]
FEE = 0.57
BE_TRIGGER = 2.00
USAGE_WEIGHTS = {4: 0.45, 5: 0.30, 6: 0.15, 7: 0.07, 8: 0.03}

# Quick exit targets (1pt T1, 2pt T2, same qty split)
QX_T1 = 1.00
QX_T2 = 2.00

# BE abort: avg loss when using BE abort
# Trade went >2pts against you, bounced back to near entry, you exit.
# Typical: -0.5 to -1.5pts on full position = avg ~-1pt loss on full qty
BE_ABORT_LOSS_PTS = 1.00   # avg loss per contract when BE abort used

def compute_outcomes(sl, t1, t2, qt1, qt2):
    fees = FEE * (qt1 + qt2)
    total = qt1 + qt2
    pnl_full    = t1*qt1*5 + t2*qt2*5 - fees
    pnl_t1_be   = t1*qt1*5 + 0 - fees
    pnl_be_only = BE_TRIGGER*total*5 - fees
    pnl_stop    = -(sl*total*5) - fees
    pnl_be_abort = -(BE_ABORT_LOSS_PTS*total*5) - fees   # exited near BE going against
    pnl_quick_exit = QX_T1*qt1*5 + QX_T2*qt2*5 - fees   # switched to 1/2pt targets
    return pnl_full, pnl_t1_be, pnl_be_only, pnl_stop, pnl_be_abort, pnl_quick_exit

SEP = "=" * 130
DIV = "-" * 130

# ── Compare 3 models side by side ────────────────────────────────────────────
print(SEP)
print("  EXPECTANCY COMPARISON: Old model vs New ATM vs New ATM + BE Abort + Quick Exit")
print(SEP)
print()

MODELS = [
    {
        "name": "MODEL 1: Old ATM (current reality from data)",
        "p_full":     0.029,
        "p_t1_be":    0.210,
        "p_be_only":  0.024,
        "p_stop":     0.119,
        "p_be_abort": 0.000,
        "p_quick":    0.000,
        "p_manual":   0.547,
        "manual_avg": -14.65,
    },
    {
        "name": "MODEL 2: New ATM only (no button intervention, target probabilities)",
        "p_full":     0.35,
        "p_t1_be":    0.25,
        "p_be_only":  0.15,
        "p_stop":     0.25,
        "p_be_abort": 0.00,
        "p_quick":    0.00,
        "p_manual":   0.00,
        "manual_avg": 0,
    },
    {
        "name": "MODEL 3: New ATM + BE Abort + Quick Exit buttons",
        "p_full":     0.35,
        "p_t1_be":    0.20,
        "p_be_only":  0.15,
        "p_stop":     0.10,   # hard stops cut from 25% to 10%
        "p_be_abort": 0.10,   # 10% of trades: went against, BE abort caught them
        "p_quick":    0.10,   # 10% of trades: switched to quick exit
        "p_manual":   0.00,
        "manual_avg": 0,
    },
]

# Sensitivity for model 3: vary BE abort success rate
BE_ABORT_SCENARIOS = [
    ("Optimistic: avg -0.50pt loss on abort",  0.50),
    ("Realistic:  avg -1.00pt loss on abort",  1.00),
    ("Pessimistic: avg -1.50pt loss on abort", 1.50),
    ("Worst case: avg -2.00pt loss on abort",  2.00),
]

for sl, t1, t2, qt1, qt2 in CONFIGS:
    total = qt1 + qt2
    fees = FEE * total
    pf, pt, pb, ps, pa, pq = compute_outcomes(sl, t1, t2, qt1, qt2)

    print(DIV)
    print(f"  SL{sl}  T1={t1:.2f}  T2={t2:.2f}  Qty={qt1}+{qt2}  "
          f"| Full=${pf:+.2f}  T1+BE=${pt:+.2f}  BEonly=${pb:+.2f}  "
          f"Stop=${ps:+.2f}  QuickExit=${pq:+.2f}")
    print(DIV)

    for model in MODELS:
        e = (model['p_full']     * pf +
             model['p_t1_be']    * pt +
             model['p_be_only']  * pb +
             model['p_stop']     * ps +
             model['p_be_abort'] * pa +
             model['p_quick']    * pq +
             model['p_manual']   * model['manual_avg'])
        print(f"  {model['name']:<60} E={e:>+8.2f}")

    print()
    print(f"  Model 3 sensitivity -- BE Abort avg loss vs E/trade:")
    for label, abort_pts in BE_ABORT_SCENARIOS:
        pa_sens = -(abort_pts * total * 5) - fees
        m3 = MODELS[2]
        e_sens = (m3['p_full']*pf + m3['p_t1_be']*pt + m3['p_be_only']*pb +
                  m3['p_stop']*ps + m3['p_be_abort']*pa_sens + m3['p_quick']*pq)
        print(f"    {label:<45} E={e_sens:>+8.2f}")
    print()

# ── System-level summary ──────────────────────────────────────────────────────
print(SEP)
print("  SYSTEM-LEVEL WEIGHTED E/TRADE COMPARISON")
print(SEP)
print(f"  {'Model':<60} {'Wtd E':>9} {'vs Old':>9}")
print(f"  {'-'*60} {'-'*9} {'-'*9}")

old_system_e = None
for model in MODELS:
    system_e = 0
    for sl, t1, t2, qt1, qt2 in CONFIGS:
        total = qt1 + qt2
        fees = FEE * total
        pf, pt, pb, ps, pa, pq = compute_outcomes(sl, t1, t2, qt1, qt2)
        e = (model['p_full']*pf + model['p_t1_be']*pt + model['p_be_only']*pb +
             model['p_stop']*ps + model['p_be_abort']*pa + model['p_quick']*pq +
             model['p_manual']*model['manual_avg'])
        system_e += USAGE_WEIGHTS[sl] * e
    vs = f"{system_e-old_system_e:>+9.2f}" if old_system_e is not None else "   baseline"
    print(f"  {model['name']:<60} {system_e:>+9.2f} {vs}")
    if old_system_e is None:
        old_system_e = system_e

print()

# Model 3 system sensitivity
print("  Model 3 system E sensitivity vs BE Abort avg loss:")
for label, abort_pts in BE_ABORT_SCENARIOS:
    system_e = 0
    m3 = MODELS[2]
    for sl, t1, t2, qt1, qt2 in CONFIGS:
        total = qt1 + qt2
        fees = FEE * total
        pf, pt, pb, ps, pa, pq = compute_outcomes(sl, t1, t2, qt1, qt2)
        pa_sens = -(abort_pts * total * 5) - fees
        e = (m3['p_full']*pf + m3['p_t1_be']*pt + m3['p_be_only']*pb +
             m3['p_stop']*ps + m3['p_be_abort']*pa_sens + m3['p_quick']*pq)
        system_e += USAGE_WEIGHTS[sl] * e
    print(f"    {label:<45} Wtd E={system_e:>+8.2f}")

print()
print(SEP)
print("  WHAT THE BUTTONS ACTUALLY BUY YOU")
print(SEP)

for sl, t1, t2, qt1, qt2 in CONFIGS:
    total = qt1 + qt2
    fees = FEE * total
    pf, pt, pb, ps, pa, pq = compute_outcomes(sl, t1, t2, qt1, qt2)

    # Value of BE abort: converts full stop into BE abort loss
    # Each D->E conversion saves: ps - pa = stop_loss - be_abort_loss
    be_abort_saves = pa - ps   # pa is less negative than ps so this is positive
    # Value of quick exit: converts dud B into quick win
    # Each B->F conversion gains: pq - pt
    quick_exit_gains = pq - pt

    print(f"  SL{sl}: BE Abort saves ${be_abort_saves:>+8.2f} per converted stop  |  "
          f"Quick Exit gains ${quick_exit_gains:>+8.2f} per converted T1+BE dud")

print()
print("  At Model 3 probabilities (10% each button used):")
for sl, t1, t2, qt1, qt2 in CONFIGS:
    total = qt1 + qt2
    fees = FEE * total
    pf, pt, pb, ps, pa, pq = compute_outcomes(sl, t1, t2, qt1, qt2)
    be_abort_saves = pa - ps
    quick_exit_gains = pq - pt
    total_button_value = 0.10*be_abort_saves + 0.10*quick_exit_gains
    print(f"  SL{sl}: buttons add ${total_button_value:>+8.2f}/trade to E  "
          f"(BE abort: ${0.10*be_abort_saves:>+7.2f}  Quick exit: ${0.10*quick_exit_gains:>+7.2f})")

print()
print(SEP)
print("  BREAKEVEN FULL-WIN RATE WITH BUTTONS (Model 3)")
print(SEP)
m3 = MODELS[2]
for sl, t1, t2, qt1, qt2 in CONFIGS:
    total = qt1 + qt2
    fees = FEE * total
    pf, pt, pb, ps, pa, pq = compute_outcomes(sl, t1, t2, qt1, qt2)
    # E = x*pf + (0.60-x)*pt + fixed_terms = 0  where fixed=0.15*pb+0.10*ps+0.10*pa+0.10*pq
    # x*(pf-pt) = -(fixed + 0.60*pt)
    remaining = m3['p_full'] + m3['p_t1_be']  # = 0.55
    fixed = m3['p_be_only']*pb + m3['p_stop']*ps + m3['p_be_abort']*pa + m3['p_quick']*pq
    # E = x*pf + (remaining-x)*pt + fixed = 0
    # x*(pf-pt) + remaining*pt + fixed = 0
    numer = -(remaining*pt + fixed)
    denom = pf - pt
    be_x = numer / denom
    be_pct_of_t1 = be_x / remaining
    print(f"  SL{sl}: need {be_x:.1%} of all trades as full wins  "
          f"({be_pct_of_t1:.1%} of T1 hits must reach T2)  "
          f"[old model needed {0.143 if sl==4 else 0.167 if sl==5 else 0.199 if sl==6 else 0.213 if sl==7 else 0.205:.1%}]")
