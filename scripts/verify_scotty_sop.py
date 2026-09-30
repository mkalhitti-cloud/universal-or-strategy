import sys, os
sys.stdout.reconfigure(encoding='utf-8')

NT_STOP = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\StopStrategy'
NT_ATM  = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'
HTML1   = r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html'
HTML2   = r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html'

print('=== 1. StopStrategy XML ===')
sop_xml = open(os.path.join(NT_STOP, 'MGC Scotty SOP.xml'), encoding='utf-8').read()
checks = [
    ('BE=10',           '<AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>'),
    ('BE+2',            '<AutoBreakEvenPlus>2</AutoBreakEvenPlus>'),
    ('Step1 @20 stop5', '<ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss>'),
    ('Step2 @30 stop5', '<ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss>'),
    ('Step3 @40 stop2', '<ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss>'),
    ('SimStop=true',    '<IsSimStopEnabled>true</IsSimStopEnabled>'),
    ('VolTrigger=1',    '<VolumeTrigger>1</VolumeTrigger>'),
]
for label, pat in checks:
    mark = 'OK' if pat in sop_xml else 'FAIL'
    print(f'  {mark}  {label}')

print()
print('=== 2. ATM XMLs - 21 MGC files ===')
mgc_files = sorted([f for f in os.listdir(NT_ATM) if f.startswith('MGC ')])
all_ok = True
for fname in mgc_files:
    c = open(os.path.join(NT_ATM, fname), encoding='utf-8').read()
    has_scotty = 'MGC Scotty SOP' in c
    has_sim    = '<IsSimStopEnabled>true</IsSimStopEnabled>' in c
    has_vol    = '<VolumeTrigger>1</VolumeTrigger>' in c
    ok = has_scotty and has_sim and has_vol
    if not ok:
        all_ok = False
    mark = 'OK' if ok else 'ERR'
    print(f'  {mark}  {fname}')
print('  -> All 21 OK:', all_ok)

print()
print('=== 3. mes-atm-grid.html ===')
h1 = open(HTML1, encoding='utf-8').read()
old_left = [x for x in ['MGC SOP3','MGC SOP35','MGC SOP4','MGC SOP45','MGC SOP5'] if x in h1]
scotty = h1.count('Scotty SOP')
print('  Old badges remaining :', old_left if old_left else 'NONE')
print('  Scotty SOP count     :', scotty)

print()
print('=== 4. apex-trading-sop.html ===')
h2 = open(HTML2, encoding='utf-8').read()
scotty_ref = 'Scotty (WSGTA)' in h2
be10 = 'Profit Trigger = <strong>10T</strong>' in h2
print('  Scotty (WSGTA) reference :', scotty_ref)
print('  BE trigger updated 10T   :', be10)

print()
print('=== SUMMARY ===')
print('All checks passed' if all_ok and not old_left and scotty_ref and be10 else 'CHECK ABOVE FOR ISSUES')
