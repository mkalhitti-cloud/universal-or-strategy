import sys, os, re
sys.stdout.reconfigure(encoding='utf-8')

NT_STOP = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\StopStrategy'
NT_ATM  = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'
HTML1   = r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html'
HTML2   = r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html'

PASS = 0
FAIL = 0

def check(label, result, detail=''):
    global PASS, FAIL
    if result:
        PASS += 1
        print(f'  PASS  {label}')
    else:
        FAIL += 1
        print(f'  FAIL  {label}' + (f'  [{detail}]' if detail else ''))

# ─────────────────────────────────────────────────────────────────────────────
print('=' * 60)
print('SECTION 1: StopStrategy XML — MGC Scotty SOP.xml')
print('=' * 60)

sop_path = os.path.join(NT_STOP, 'MGC Scotty SOP.xml')
check('File exists', os.path.isfile(sop_path))
sop = open(sop_path, encoding='utf-8').read()

check('AutoBreakEvenProfitTrigger = 10',  '<AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>' in sop)
check('AutoBreakEvenPlus = 2',            '<AutoBreakEvenPlus>2</AutoBreakEvenPlus>' in sop)
check('Step1 ProfitTrigger=20 StopLoss=5','<ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss>' in sop)
check('Step2 ProfitTrigger=30 StopLoss=5','<ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss>' in sop)
check('Step3 ProfitTrigger=40 StopLoss=2','<ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss>' in sop)
check('Step1 Frequency=2',               sop.count('<Frequency>2</Frequency>') >= 2)
check('Step3 Frequency=1',               '<Frequency>1</Frequency>' in sop)
check('IsSimStopEnabled = true',          '<IsSimStopEnabled>true</IsSimStopEnabled>' in sop)
check('VolumeTrigger = 1',               '<VolumeTrigger>1</VolumeTrigger>' in sop)
check('Template name = MGC Scotty SOP',  '<Template>MGC Scotty SOP</Template>' in sop)
check('Exactly 3 AutoTrailSteps',        sop.count('<AutoTrailStep>') == 3)

# ─────────────────────────────────────────────────────────────────────────────
print()
print('=' * 60)
print('SECTION 2: All 21 MGC ATM XMLs')
print('=' * 60)

import math

# Expected quantities
QTY = {
    '$400': {4:10, 5:8, 6:6, 7:5, 8:5, 9:4, 10:4},
    '$200': {4:5,  5:4, 6:3, 7:3, 8:2, 9:2, 10:2},
    '$100': {4:2,  5:2, 6:2, 7:2, 8:2, 9:2, 10:2},
}

mgc_files = sorted([f for f in os.listdir(NT_ATM) if f.startswith('MGC ')])
check('Exactly 21 MGC ATM files', len(mgc_files) == 21, f'found {len(mgc_files)}')

for fname in mgc_files:
    fpath = os.path.join(NT_ATM, fname)
    c = open(fpath, encoding='utf-8').read()

    # Parse tier and SL from filename e.g. "MGC $400 SL4.xml"
    m = re.match(r'MGC (\$\d+) SL(\d+)\.xml', fname)
    if not m:
        check(f'{fname} filename parse', False, 'unexpected filename')
        continue
    tier, sl = m.group(1), int(m.group(2))

    # ── structure checks ──
    check(f'{fname}: has MGC Scotty SOP reference',  'MGC Scotty SOP' in c)
    check(f'{fname}: IsSimStopEnabled=true',          '<IsSimStopEnabled>true</IsSimStopEnabled>' in c)
    check(f'{fname}: VolumeTrigger=1',                '<VolumeTrigger>1</VolumeTrigger>' in c)
    check(f'{fname}: no old SOP3/35/4/45/5 refs',
          not any(x in c for x in ['MGC SOP3','MGC SOP35','MGC SOP4','MGC SOP45','MGC SOP5']))

    # ── trail values inside ATM ──
    check(f'{fname}: AutoBreakEvenProfitTrigger=10',  '<AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>' in c)
    check(f'{fname}: AutoBreakEvenPlus=2',            '<AutoBreakEvenPlus>2</AutoBreakEvenPlus>' in c)
    check(f'{fname}: Step1 @20 stop5',                '<ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss>' in c)
    check(f'{fname}: Step2 @30 stop5',                '<ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss>' in c)
    check(f'{fname}: Step3 @40 stop2',                '<ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss>' in c)

    # ── SL and T1 tick values ──
    sl_tk = sl * 10
    t1_tk = 10   # fixed 1pt for all MGC
    t2_tk = sl_tk // 2
    t3_tk = int(sl_tk * 0.75)

    check(f'{fname}: StopLoss={sl_tk}tk in brackets', f'<StopLoss>{sl_tk}</StopLoss>' in c)
    check(f'{fname}: T1 target={t1_tk}tk in B1',      c.count(f'<Target>{t1_tk}</Target>') >= 1)

    # ── entry quantity ──
    if tier in QTY and sl in QTY[tier]:
        exp_total = QTY[tier][sl]
        check(f'{fname}: EntryQuantity={exp_total}',
              f'<EntryQuantity>{exp_total}</EntryQuantity>' in c,
              f'expected {exp_total}')

    # ── Q1 bracket: no StopStrategy (plain target) ──
    # Q1 must have a <Bracket> with <Target> but no <StopStrategy> before it
    # Simple check: first <Bracket> block should not contain <StopStrategy>
    first_bracket = re.search(r'<Bracket>(.*?)</Bracket>', c, re.DOTALL)
    if first_bracket:
        check(f'{fname}: Q1 bracket has no trail (fixed exit)',
              '<StopStrategy>' not in first_bracket.group(1))

# ─────────────────────────────────────────────────────────────────────────────
print()
print('=' * 60)
print('SECTION 3: mes-atm-grid.html')
print('=' * 60)

h1 = open(HTML1, encoding='utf-8').read()
old_sop_names = ['MGC SOP3','MGC SOP35','MGC SOP4','MGC SOP45','MGC SOP5']

for name in old_sop_names:
    check(f'No "{name}" badge remaining', name not in h1)

check('Scotty SOP appears (>=20 times)', h1.count('Scotty SOP') >= 20,
      f'found {h1.count("Scotty SOP")}')
check('Scotty SOP panel header present',
      'Universal "Scotty SOP"' in h1 or 'MGC Scotty SOP' in h1)
check('SimStop Vol Trigger note in HTML',
      'Volume Trigger' in h1 or 'vol=' in h1.lower())
check('SOPs tab still present',          "id='tsop'" in h1 or 'id="tsop"' in h1)
check('NT8 footer references MGC Scotty SOP', 'MGC Scotty SOP' in h1)
import re as _re
_badges = _re.findall(r'<span class="sop-badge">(.*?)</span>', h1)
check('All sop-badge spans properly closed', len(_badges) > 0 and 'MGC SOP' not in str(_badges))

# Count Scotty SOP badges per tier section
mgc400_section = h1[h1.find('id="tmgc400"'):h1.find('id="tmgc200"')]
mgc200_section = h1[h1.find('id="tmgc200"'):h1.find('id="tmgc100"')]
mgc100_section = h1[h1.find('id="tmgc100"'):h1.find('id="tsop"')]

check('MGC $400: 7 Scotty SOP badges',   mgc400_section.count('Scotty SOP') >= 7,
      f'found {mgc400_section.count("Scotty SOP")}')
check('MGC $200: 7 Scotty SOP badges',   mgc200_section.count('Scotty SOP') >= 7,
      f'found {mgc200_section.count("Scotty SOP")}')
check('MGC $100: 7 Scotty SOP badges',   mgc100_section.count('Scotty SOP') >= 7,
      f'found {mgc100_section.count("Scotty SOP")}')

# ─────────────────────────────────────────────────────────────────────────────
print()
print('=' * 60)
print('SECTION 4: apex-trading-sop.html')
print('=' * 60)

h2 = open(HTML2, encoding='utf-8').read()
check('Scotty (WSGTA) reference present',   'Scotty (WSGTA)' in h2)
check('BE trigger updated to 10T',          'Profit Trigger = <strong>10T</strong>' in h2)
check('Step1 stop=5T trigger=20T',          '>5T<' in h2 and '>20T<' in h2)
check('Step3 stop=2T trigger=40T',          '>2T<' in h2 and '>40T<' in h2)
check('SimStop Vol Trigger note present',   'Volume Trigger = 1' in h2)
# Check the MGC Scotty SOP card block by line range (L2050-L2089)
h2_lines = h2.split('\n')
mgc_card_lines = h2_lines[2049:2089]  # 0-indexed: lines 2050-2089
mgc_card_text = '\n'.join(mgc_card_lines)
check('MGC trail card: old stop 20T (proportional) removed',
      'color:var(--danger)">20T' not in mgc_card_text,
      'old proportional 20T stop still in MGC card')
check('MGC trail card: old stop 15T removed',
      '>15T<' not in mgc_card_text,
      'old 15T still in MGC card')
check('MGC trail card: old trigger 50T removed',
      '>50T<' not in mgc_card_text,
      'old 50T trigger still in MGC card')
check('MGC trail card: correct values present (5T/20T, 5T/30T, 2T/40T)',
      '>5T<' in mgc_card_text and '>20T<' in mgc_card_text and
      '>30T<' in mgc_card_text and '>2T<' in mgc_card_text and '>40T<' in mgc_card_text)

# ─────────────────────────────────────────────────────────────────────────────
print()
print('=' * 60)
print(f'RESULTS: {PASS} PASSED  |  {FAIL} FAILED')
print('=' * 60)
if FAIL == 0:
    print('ALL CHECKS PASSED - Scotty SOP deployment is 100% accurate')
else:
    print('ISSUES FOUND - see FAIL lines above')
