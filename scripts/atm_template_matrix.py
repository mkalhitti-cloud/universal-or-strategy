import math

tick_val = 1.25
fee_rt   = 0.57
sls      = [4, 5, 6, 7, 8]
weights  = {4: 0.45, 5: 0.30, 6: 0.15, 7: 0.07, 8: 0.03}

def max_qty(sl): return int(400 / (sl * 5))
def p(pts, qty): return round(pts * 4 * tick_val * qty, 2)
def f(qty):      return round(fee_rt * qty, 2)
def t(sl, pct):  return round(sl * pct, 2)

# ── CONTRACT SPLITS ──────────────────────────────────────────────
def split2(total, heavy):
    if heavy == 0:
        q1 = total // 2; q2 = total - q1
    elif heavy == 1:
        q1 = max(1, round(total * 0.65)); q2 = total - q1
    else:
        q2 = max(1, round(total * 0.65)); q1 = total - q2
    return q1, q2

def split3(total, heavy):
    base = total // 3; rem = total - base * 3
    if heavy == 0:
        q = [base] * 3
        for i in range(rem): q[i] += 1
    elif heavy == 1:
        q1 = max(1, round(total * 0.50)); rest = total - q1
        q2 = rest // 2; q3 = rest - q2; q = [q1, q2, q3]
    elif heavy == 2:
        q2 = max(1, round(total * 0.50)); rest = total - q2
        q1 = rest // 2; q3 = rest - q1; q = [q1, q2, q3]
    else:
        q3 = max(1, round(total * 0.50)); rest = total - q3
        q1 = rest // 2; q2 = rest - q1; q = [q1, q2, q3]
    return q[0], q[1], q[2]

# ── TRAIL SOPs ───────────────────────────────────────────────────
# Each step: (trigger_pt, trail_ticks_in_pts) -> stop = trigger - trail
trail_sops = {
    'SOP3':  [(3.0, 2.0), (4.0, 1.5), (5.0, 1.0)],
    'SOP35': [(3.5, 2.0), (4.5, 1.5), (5.5, 1.0)],
    'SOP4':  [(4.0, 2.0), (5.0, 1.5), (6.0, 1.0)],
}

base_probs = {
    'full':    0.20,
    'trail1':  0.10,
    'trail2':  0.10,
    'trail3':  0.10,
    'auto_be': 0.10,
    'stop':    0.10,
    'quick':   0.10,
    'be_btn':  0.20,
}

# ── TRAIL PnL HELPER ─────────────────────────────────────────────
def trail_catch_pnl(legs, trigger, trail_stop, fee_val):
    pnl = 0
    for qty, tv in legs:
        if tv <= trigger:
            pnl += p(tv, qty)
        else:
            pnl += p(trail_stop, qty)
    return round(pnl - fee_val, 2)

# ── 2-TARGET E/TRADE ─────────────────────────────────────────────
def compute_e_2t(sl, t1p, t2p, heavy, sop_name):
    total = max_qty(sl)
    q1, q2 = split2(total, heavy)
    fee = f(total)
    t1v = t(sl, t1p); t2v = t(sl, t2p)
    steps = trail_sops[sop_name]
    stops = [round(s[0] - s[1], 2) for s in steps]
    legs  = [(q1, t1v), (q2, t2v)]

    full     = p(t1v, q1) + p(t2v, q2) - fee
    trail1   = trail_catch_pnl(legs, steps[0][0], stops[0], fee)
    trail2   = trail_catch_pnl(legs, steps[1][0], stops[1], fee)
    trail3   = trail_catch_pnl(legs, steps[2][0], stops[2], fee)
    auto_be  = (p(t1v, q1) + p(0.25, q2) - fee) if t1v <= 2.0 else (p(0.25, total) - fee)
    hard_stop = -p(sl, total) - fee
    quick    = p(1.0, q1) + p(2.0, q2) - fee
    be_btn   = 0.40 * (0 - fee) + 0.60 * hard_stop

    e = (base_probs['full']    * full
       + base_probs['trail1']  * trail1
       + base_probs['trail2']  * trail2
       + base_probs['trail3']  * trail3
       + base_probs['auto_be'] * auto_be
       + base_probs['stop']    * hard_stop
       + base_probs['quick']   * quick
       + base_probs['be_btn']  * be_btn)

    detail = dict(full=full, trail1=trail1, trail2=trail2, trail3=trail3,
                  auto_be=auto_be, stop=round(hard_stop,2), quick=quick,
                  be_btn=round(be_btn,2), q1=q1, q2=q2, t1v=t1v, t2v=t2v,
                  total=total, fee=fee)
    return round(e, 2), detail

# ── 3-TARGET E/TRADE ─────────────────────────────────────────────
def compute_e_3t(sl, t1p, t2p, t3p, heavy, sop_name):
    total = max_qty(sl)
    q1, q2, q3 = split3(total, heavy)
    fee = f(total)
    t1v = t(sl, t1p); t2v = t(sl, t2p); t3v = t(sl, t3p)
    steps = trail_sops[sop_name]
    stops = [round(s[0] - s[1], 2) for s in steps]
    legs  = [(q1, t1v), (q2, t2v), (q3, t3v)]

    full      = p(t1v,q1) + p(t2v,q2) + p(t3v,q3) - fee
    trail1    = trail_catch_pnl(legs, steps[0][0], stops[0], fee)
    trail2    = trail_catch_pnl(legs, steps[1][0], stops[1], fee)
    trail3    = trail_catch_pnl(legs, steps[2][0], stops[2], fee)

    if t1v <= 2.0:
        done = [(qty,tv) for qty,tv in legs if tv <= 2.0]
        open_ = [(qty,tv) for qty,tv in legs if tv > 2.0]
        auto_be = sum(p(tv,qty) for qty,tv in done) + sum(p(0.25,qty) for qty,_ in open_) - fee
    else:
        auto_be = p(0.25, total) - fee

    hard_stop = -p(sl, total) - fee
    quick     = p(1.0, q1) + p(2.0, q2) + p(2.0, q3) - fee
    be_btn    = 0.40 * (0 - fee) + 0.60 * hard_stop

    e = (base_probs['full']    * full
       + base_probs['trail1']  * trail1
       + base_probs['trail2']  * trail2
       + base_probs['trail3']  * trail3
       + base_probs['auto_be'] * auto_be
       + base_probs['stop']    * hard_stop
       + base_probs['quick']   * quick
       + base_probs['be_btn']  * be_btn)

    detail = dict(full=full, trail1=trail1, trail2=trail2, trail3=trail3,
                  auto_be=auto_be, stop=round(hard_stop,2), quick=quick,
                  be_btn=round(be_btn,2), q1=q1, q2=q2, q3=q3,
                  t1v=t1v, t2v=t2v, t3v=t3v, total=total, fee=fee)
    return round(e, 2), detail

# ── TEMPLATE REGISTRY ────────────────────────────────────────────
templates_2t = {}
for t1p, t2p in [(0.5, 0.75), (0.5, 1.0), (0.75, 1.0)]:
    t1k = '5' if t1p == 0.5 else '75'
    t2k = '75' if t2p == 0.75 else '1'
    for h in [0, 1, 2]:
        name = '2-' + t1k + '-' + t2k + '-' + str(h)
        templates_2t[name] = (t1p, t2p, h)

templates_3t = {}
for h in [0, 1, 2, 3]:
    name = '3-5-75-1-' + str(h)
    templates_3t[name] = (0.5, 0.75, 1.0, h)

# ── PRINT HEADER ─────────────────────────────────────────────────
sep = '=' * 80

print(sep)
print('ATM TEMPLATE NAMING CONVENTION')
print(sep)
print('  Format : [targets]-[T1%SL]-[T2%SL]-([T3%SL]-)[heavy]')
print('  %SL    : 5=50%SL  75=75%SL  1=100%SL')
print('  heavy  : 0=equal  1=heavyT1  2=heavyT2  3=heavyT3')
print('  SOP    : SOP3=trail@3/4/5  SOP35=trail@3.5/4.5/5.5  SOP4=trail@4/5/6')
print('  BE auto: always 2pt trigger -> stop +1tk (unchanged)')
print()
print('  2-target splits: equal=50/50  heavyT1=65/35  heavyT2=35/65')
print('  3-target splits: equal=~33ea  heavyT1=50/25/25  heavyT2=25/50/25  heavyT3=25/25/50')
print()

# ── CONTRACT SPLITS TABLE ────────────────────────────────────────
print(sep)
print('CONTRACT SPLITS PER SL')
print(sep)
header = '{:<22} {:>10} {:>10} {:>10} {:>10} {:>10}'.format(
    'Template', 'SL4(20x)', 'SL5(16x)', 'SL6(13x)', 'SL7(11x)', 'SL8(10x)')
print(header)
print('-' * 75)
for tname, (t1p, t2p, h) in templates_2t.items():
    row = []
    for sl in sls:
        tot = max_qty(sl)
        q1, q2 = split2(tot, h)
        t1v = t(sl, t1p); t2v = t(sl, t2p)
        row.append(str(q1) + '+' + str(q2) + ' (' + str(t1v) + '/' + str(t2v) + ')')
    print('{:<22} {:>10} {:>10} {:>10} {:>10} {:>10}'.format(tname, *[r.split(' ')[0] for r in row]))

print()
for tname, (t1p, t2p, t3p, h) in templates_3t.items():
    row = []
    for sl in sls:
        tot = max_qty(sl)
        q1, q2, q3 = split3(tot, h)
        row.append(str(q1) + '+' + str(q2) + '+' + str(q3))
    print('{:<22} {:>10} {:>10} {:>10} {:>10} {:>10}'.format(tname, *row))

# ── TARGET LEVELS TABLE ──────────────────────────────────────────
print()
print(sep)
print('TARGET LEVELS (pts from entry) BY SL')
print(sep)
header2 = '{:<22} {:>12} {:>12} {:>12} {:>12} {:>12}'.format(
    'Template', 'SL4', 'SL5', 'SL6', 'SL7', 'SL8')
print(header2)
print('-' * 80)
for tname, (t1p, t2p, h) in templates_2t.items():
    row = []
    for sl in sls:
        row.append('T1=' + str(t(sl,t1p)) + ' T2=' + str(t(sl,t2p)))
    print('{:<22} {:>12} {:>12} {:>12} {:>12} {:>12}'.format(
        tname, *[r.replace(' T2=', '/') for r in row]))

print()
for tname, (t1p, t2p, t3p, h) in templates_3t.items():
    row = []
    for sl in sls:
        row.append(str(t(sl,t1p)) + '/' + str(t(sl,t2p)) + '/' + str(t(sl,t3p)))
    print('{:<22} {:>12} {:>12} {:>12} {:>12} {:>12}'.format(tname, *row))

# ── SYSTEM E/TRADE MATRIX ────────────────────────────────────────
print()
print(sep)
print('SYSTEM WEIGHTED E/TRADE  (SL4=45% SL5=30% SL6=15% SL7=7% SL8=3%)')
print('Scenario probs: Full=20% Trail1=10% Trail2=10% Trail3=10% AutoBE=10%')
print('                HardStop=10% Quick=10% BEbtn=20%')
print(sep)
print('{:<22} {:>10} {:>10} {:>10}  Best'.format('Template', 'SOP3', 'SOP35', 'SOP4'))
print('-' * 65)

all_results = {}
for tname, (t1p, t2p, h) in templates_2t.items():
    row = {}
    for sop in ['SOP3', 'SOP35', 'SOP4']:
        sys_e = sum(weights[sl] * compute_e_2t(sl, t1p, t2p, h, sop)[0] for sl in sls)
        row[sop] = round(sys_e, 2)
    all_results[tname] = row
    best_sop = max(row, key=row.get)
    print('{:<22} {:>+10.2f} {:>+10.2f} {:>+10.2f}  {}={:+.2f}'.format(
        tname, row['SOP3'], row['SOP35'], row['SOP4'], best_sop, row[best_sop]))

print()
for tname, (t1p, t2p, t3p, h) in templates_3t.items():
    row = {}
    for sop in ['SOP3', 'SOP35', 'SOP4']:
        sys_e = sum(weights[sl] * compute_e_3t(sl, t1p, t2p, t3p, h, sop)[0] for sl in sls)
        row[sop] = round(sys_e, 2)
    all_results[tname] = row
    best_sop = max(row, key=row.get)
    print('{:<22} {:>+10.2f} {:>+10.2f} {:>+10.2f}  {}={:+.2f}'.format(
        tname, row['SOP3'], row['SOP35'], row['SOP4'], best_sop, row[best_sop]))

# ── SIMPLE RANKING ───────────────────────────────────────────────
print()
print(sep)
print('SIMPLE RANKING: Best E/trade per template (across all SOPs)')
print(sep)
ranked = sorted(
    [(tname, max(row.values()), max(row, key=row.get)) for tname, row in all_results.items()],
    key=lambda x: -x[1]
)
print('{:<5} {:<22} {:<8} {:<8}'.format('Rank', 'Template', 'BestSOP', 'E/trade'))
print('-' * 50)
for i, (tname, best_e, best_sop) in enumerate(ranked, 1):
    marker = ' <-- current approved' if tname == '2-5-75-0' else ''
    print('{:<5} {:<22} {:<8} {:>+8.2f}{}'.format(i, tname, best_sop, best_e, marker))
