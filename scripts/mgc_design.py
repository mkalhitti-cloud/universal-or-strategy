import sys
sys.stdout.reconfigure(encoding='utf-8')

# MGC contract spec
TICK_VAL = 1.0    # $1/tick
TICKS_PT = 10     # 10 ticks/point
PT_VAL   = 10.0   # $10/point
FEE      = 1.50   # per contract RT

# ATMs: (SL_pts, total_400, total_200)
ATMS_RAW = [
    (4,  10, 5),
    (5,   8, 4),
    (6,   6, 3),
    (7,   5, 3),
    (8,   5, 2),
    (9,   4, 2),
    (10,  4, 2),
]

import math

print('=== MGC 3-TARGET ATM DESIGN ===\n')
print(f'Contract: ${PT_VAL}/pt  ${TICK_VAL}/tick  {TICKS_PT}tk/pt  fee=${FEE}/RT\n')

for version, idx in [('$400', 1), ('$200', 2)]:
    print(f'\n--- {version} ---')
    print(f"{'SL':>4} {'SLtk':>5} {'MaxR':>6} {'Total':>5} {'Q1':>3} {'Q2':>3} {'Q3':>3}  "
          f"{'T1tk':>5} {'T1pt':>6}  {'T2tk':>5} {'T2pt':>7}  {'T3tk':>5} {'T3pt':>6}  "
          f"{'BEtk':>5} {'SOP':>6}  {'Fees':>7}")
    for sl_pts, tot400, tot200 in ATMS_RAW:
        total = tot400 if idx == 1 else tot200
        sl_tk = sl_pts * TICKS_PT
        max_risk = total * sl_tk * TICK_VAL
        # targets
        t1_tk = sl_tk // 2
        t2_tk = round(sl_tk * 0.75)
        t3_tk = sl_tk
        t1_pt = t1_tk / TICKS_PT
        t2_pt = t2_tk / TICKS_PT
        t3_pt = t3_tk / TICKS_PT
        # quantities: ceiling split
        q1 = math.ceil(total / 2)
        q2 = math.ceil((total - q1) / 2)
        q3 = total - q1 - q2
        # fees
        fees = total * FEE
        # SOP
        sop_map = {4:'SOP3',5:'SOP3',6:'SOP3',7:'SOP35',8:'SOP4',9:'SOP45',10:'SOP5'}
        sop = sop_map[sl_pts]
        print(f"SL{sl_pts:>2} {sl_tk:>5} {max_risk:>6.0f} {total:>5} {q1:>3} {q2:>3} {q3:>3}  "
              f"{t1_tk:>5} {t1_pt:>6.2f}pt  {t2_tk:>5} {t2_pt:>7.3f}pt  {t3_tk:>5} {t3_pt:>6.2f}pt  "
              f"{t1_tk:>5} {sop:>6}  {fees:>7.2f}")

print('\n\n=== SCENARIO PnL (net after fees) ===')
PROBS = {
    'Full':0.20,'Trail1':0.10,'Trail2':0.10,'Trail3':0.10,
    'AutoBE':0.10,'HardStop':0.10,'Quick':0.10,'BEbtn':0.20
}

for version, idx in [('$400', 1), ('$200', 2)]:
    print(f'\n--- {version} SCENARIO PnL ---')
    print(f"{'SL':>4} {'SOP':>6}  {'FullWin':>9} {'Trail1':>9} {'Trail2':>9} {'Trail3':>9} "
          f"{'AutoBE':>9} {'HardStop':>10} {'Quick':>9} {'BEbtn':>9}  {'E/trade':>9}")
    etrades = []
    for sl_pts, tot400, tot200 in ATMS_RAW:
        total = tot400 if idx == 1 else tot200
        sl_tk = sl_pts * TICKS_PT
        t1_tk = sl_tk // 2
        t2_tk = round(sl_tk * 0.75)
        t3_tk = sl_tk
        q1 = math.ceil(total / 2)
        q2 = math.ceil((total - q1) / 2)
        q3 = total - q1 - q2
        fees = total * FEE
        sop_map = {4:'SOP3',5:'SOP3',6:'SOP3',7:'SOP35',8:'SOP4',9:'SOP45',10:'SOP5'}
        sop = sop_map[sl_pts]

        # Full win: all 3 targets hit
        full = (q1*t1_tk + q2*t2_tk + q3*t3_tk) * TICK_VAL - fees

        # Trail1: Q2/Q3 stopped at trail step1 stop price
        # SOP trail step1 stop ≈ T1-4ticks (same ratio as MES)
        # For MGC: trail step1 stop = t1_tk - 4tk
        tr1_stop_tk = t1_tk - 4  # conservative: stops just under T1
        trail1 = (q1*t1_tk + (q2+q3)*tr1_stop_tk) * TICK_VAL - fees

        # Trail2: Q3 stopped at trail step2 stop
        tr2_stop_tk = t2_tk - 6
        trail2 = (q1*t1_tk + q2*t2_tk + q3*tr2_stop_tk) * TICK_VAL - fees

        # Trail3: all targets hit essentially = close to full win
        trail3 = full  # trail3 stop is just below T3

        # AutoBE: Q1 at T1, remainder at BE+2ticks
        be_ticks = 2  # +2tk = +0.20pt for MGC
        auto_be = (q1*t1_tk + (q2+q3)*be_ticks) * TICK_VAL - fees

        # Hard stop
        hard_stop = -(total * sl_tk * TICK_VAL) - fees

        # Quick exit: Q1 at T1, rest at ~T1/2
        quick_tks = t1_tk // 2
        quick = (q1*t1_tk + (q2+q3)*quick_tks) * TICK_VAL - fees

        # BE button: 40% scratch (-fees only), 60% full hard stop
        be_btn = 0.40*(-fees) + 0.60*(hard_stop)

        # Expectancy
        et = (PROBS['Full']*full + PROBS['Trail1']*trail1 + PROBS['Trail2']*trail2 +
              PROBS['Trail3']*trail3 + PROBS['AutoBE']*auto_be +
              PROBS['HardStop']*hard_stop + PROBS['Quick']*quick +
              PROBS['BEbtn']*be_btn)
        etrades.append((sl_pts, et, tot400 if idx==1 else tot200))

        def fmt(v): return f'+${v:.2f}' if v >= 0 else f'-${abs(v):.2f}'
        print(f"SL{sl_pts:>2} {sop:>6}  {fmt(full):>9} {fmt(trail1):>9} {fmt(trail2):>9} {fmt(trail3):>9} "
              f"{fmt(auto_be):>9} {fmt(hard_stop):>10} {fmt(quick):>9} {fmt(be_btn):>9}  {fmt(et):>9}")

    # System weighted E
    weights = {4:0.45, 5:0.30, 6:0.15, 7:0.07, 8:0.03}
    sys_e = sum(w * et for sl, et, _ in etrades[:5] for k, w in weights.items() if k == sl)
    print(f"  SYSTEM WEIGHTED E/trade (SL4=45% SL5=30% SL6=15% SL7=7% SL8=3%): {fmt(sys_e)}")
