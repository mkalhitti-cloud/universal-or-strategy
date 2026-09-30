import os, sys
sys.stdout.reconfigure(encoding='utf-8')
from xml.etree import ElementTree as ET

NT_ATM  = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'
NT_STOP = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\StopStrategy'

errors = []
notes  = []

# ── Expected values per SL ────────────────────────────────────────────────────
def expected(sl_pts):
    sl_tk = sl_pts * 4
    s1t = round(sl_tk * 24 / 48)
    s2t = round(sl_tk * 40 / 48)
    s3t = sl_tk
    return sl_tk, s1t, s2t, s3t

# Verified quantities from original ATMs
QTY = {
    400: {4:(10,5,5), 5:(8,4,4), 6:(7,3,3), 7:(6,3,2), 8:(5,3,2), 9:(4,2,2), 10:(4,2,2)},
    200: {4:(5,3,2),  5:(4,2,2), 6:(4,2,1), 7:(3,2,1), 8:(3,1,1), 9:(3,1,1), 10:(2,1,1)},
    100: {4:(3,1,1),  5:(2,1,1), 6:(2,1,1), 7:(1,1,1), 8:(1,1,1), 9:(1,1,1), 10:(1,1,1)},
}

# ── VALIDATE STOP STRATEGY FILES ─────────────────────────────────────────────
print('=== STOP STRATEGY FILES (7) ===')
for sl_pts in range(4, 11):
    name = f'Scotty-SL{sl_pts}'
    fpath = os.path.join(NT_STOP, f'{name}.xml')
    sl_tk, s1t, s2t, s3t = expected(sl_pts)
    
    if not os.path.exists(fpath):
        errors.append(f'MISSING stop: {name}.xml')
        continue
    
    tree = ET.parse(fpath)
    ss = tree.getroot().find('StopStrategy')
    
    chk = [
        (ss.find('AutoBreakEvenPlus').text,           '0',    'BEplus'),
        (ss.find('AutoBreakEvenProfitTrigger').text,  '4',    'BEtrig'),
        (ss.find('Template').text,                    name,   'Template'),
    ]
    steps = ss.findall('AutoTrailSteps/AutoTrailStep')
    if len(steps) == 3:
        chk += [
            (steps[0].find('ProfitTrigger').text, str(s1t), 'S1trig'),
            (steps[0].find('StopLoss').text,      '12',     'S1stop'),
            (steps[0].find('Frequency').text,     '4',      'S1freq'),
            (steps[1].find('ProfitTrigger').text, str(s2t), 'S2trig'),
            (steps[1].find('StopLoss').text,      '8',      'S2stop'),
            (steps[1].find('Frequency').text,     '2',      'S2freq'),
            (steps[2].find('ProfitTrigger').text, str(s3t), 'S3trig'),
            (steps[2].find('StopLoss').text,      '2',      'S3stop'),
            (steps[2].find('Frequency').text,     '1',      'S3freq'),
        ]
    else:
        errors.append(f'{name}: steps={len(steps)} (want 3)')
    
    chk += [
        (ss.find('IsSimStopEnabled').text, 'true', 'SimStop'),
        (ss.find('VolumeTrigger').text,    '1',    'VolTrig'),
    ]
    file_errors = []
    for got, want, label in chk:
        if got != want:
            file_errors.append(f'{label}={got}(want {want})')

    status = 'OK  ' if not file_errors else 'FAIL'
    sim = ss.find('IsSimStopEnabled').text
    vt  = ss.find('VolumeTrigger').text
    print(f'  {status}  {name}:  BEtrig=4+0  S1={s1t}->12x4  S2={s2t}->8x2  S3={s3t}->2x1  SimStop={sim}  VolTrig={vt}')
    errors.extend([f'{name}: {e}' for e in file_errors])

# ── VALIDATE ATM FILES ────────────────────────────────────────────────────────
print('\n=== ATM FILES (21) ===')
for risk in [400, 200, 100]:
    for sl_pts in range(4, 11):
        fname = f'MES ${risk} SL{sl_pts}.xml'
        fpath = os.path.join(NT_ATM, fname)
        sl_tk, s1t, s2t, s3t = expected(sl_pts)
        exp_q1, exp_q2, exp_q3 = QTY[risk][sl_pts]
        exp_total = exp_q1 + exp_q2 + exp_q3
        exp_t2 = sl_tk // 2
        exp_t3 = int(sl_tk * 0.75)
        
        if not os.path.exists(fpath):
            errors.append(f'MISSING ATM: {fname}')
            print(f'  MISS  {fname}')
            continue
        
        tree = ET.parse(fpath)
        atm  = tree.getroot().find('AtmStrategy')
        brackets = atm.find('Brackets').findall('Bracket')
        total = int(atm.find('EntryQuantity').text)
        
        file_errors = []
        
        # Total qty
        if total != exp_total:
            file_errors.append(f'total={total}(want {exp_total})')
        
        # B1 — T1, no stop strategy
        b1 = brackets[0]
        if int(b1.find('Target').text)   != 4:     file_errors.append(f'B1 target={b1.find("Target").text}(want 4)')
        if int(b1.find('StopLoss').text) != sl_tk: file_errors.append(f'B1 SL={b1.find("StopLoss").text}(want {sl_tk})')
        if int(b1.find('Quantity').text) != exp_q1: file_errors.append(f'B1 qty={b1.find("Quantity").text}(want {exp_q1})')
        if b1.find('StopStrategy') is not None:     file_errors.append('B1 has StopStrategy (should be none)')
        
        # B2/B3 — Scotty trail
        for bi, (b, exp_q, exp_t) in enumerate([(brackets[1], exp_q2, exp_t2), (brackets[2] if len(brackets)>2 else None, exp_q3, exp_t3)], 2):
            if b is None:
                if exp_q > 0: file_errors.append(f'B{bi} missing (exp_q={exp_q})')
                continue
            ss = b.find('StopStrategy')
            if ss is None:
                file_errors.append(f'B{bi} missing StopStrategy')
                continue
            chk = [
                (int(b.find('Quantity').text),                        exp_q,  f'B{bi} qty'),
                (int(b.find('Target').text),                          exp_t,  f'B{bi} target'),
                (int(b.find('StopLoss').text),                        sl_tk,  f'B{bi} SL'),
                (int(ss.find('AutoBreakEvenPlus').text),              0,      f'B{bi} BEplus'),
                (int(ss.find('AutoBreakEvenProfitTrigger').text),     4,      f'B{bi} BEtrig'),
                (ss.find('Template').text,                            f'Scotty-SL{sl_pts}', f'B{bi} tmpl'),
                (ss.find('IsSimStopEnabled').text,                    'true', f'B{bi} SimStop'),
                (ss.find('VolumeTrigger').text,                       '1',    f'B{bi} VolTrig'),
            ]
            steps = ss.findall('AutoTrailSteps/AutoTrailStep')
            if len(steps) == 3:
                chk += [
                    (int(steps[0].find('ProfitTrigger').text), s1t, f'B{bi} S1trig'),
                    (int(steps[0].find('StopLoss').text),      12,  f'B{bi} S1stop'),
                    (int(steps[0].find('Frequency').text),      4,  f'B{bi} S1freq'),
                    (int(steps[1].find('ProfitTrigger').text), s2t, f'B{bi} S2trig'),
                    (int(steps[1].find('StopLoss').text),       8,  f'B{bi} S2stop'),
                    (int(steps[1].find('Frequency').text),      2,  f'B{bi} S2freq'),
                    (int(steps[2].find('ProfitTrigger').text), s3t, f'B{bi} S3trig'),
                    (int(steps[2].find('StopLoss').text),       2,  f'B{bi} S3stop'),
                    (int(steps[2].find('Frequency').text),      1,  f'B{bi} S3freq'),
                ]
            else:
                file_errors.append(f'B{bi} steps={len(steps)}(want 3)')
            for got, want, label in chk:
                if got != want:
                    file_errors.append(f'{label}={got}(want {want})')
        
        status = 'OK  ' if not file_errors else 'FAIL'
        q_str = f'{exp_q1}+{exp_q2}+{exp_q3}'
        print(f'  {status}  {fname:<22}  total={total:>2}  Q={q_str}  SL={sl_tk}tk  T=4/{exp_t2}/{exp_t3}  S1={s1t}->12  S2={s2t}->8  S3={s3t}->2')
        errors.extend([f'{fname}: {e}' for e in file_errors])

# ── SUMMARY ───────────────────────────────────────────────────────────────────
print(f'\n{"="*60}')
if errors:
    print(f'VALIDATION FAILED — {len(errors)} error(s):')
    for e in errors: print(f'  !! {e}')
else:
    print('ALL 28 FILES PASS — 100% correct')
print(f'{"="*60}')

# ── SIM STOP QUESTION ─────────────────────────────────────────────────────────
print('\nNOTE: Scotty image shows IsSimStopEnabled=true, VolumeTrigger=1')
print('Current: IsSimStopEnabled=false, VolumeTrigger=0 in all Scotty-SL*.xml')
print('Confirm: do you want SimStop ENABLED (true, VT=1) on all 7 stop strategies?')
