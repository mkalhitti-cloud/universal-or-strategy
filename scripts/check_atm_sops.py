import sys, os, re
sys.stdout.reconfigure(encoding='utf-8')

NT_ATM = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

targets = [f for f in os.listdir(NT_ATM)
           if (f.startswith('MES $') or f.startswith('MGC $')) and f.endswith('.xml')]
targets.sort()

for fname in targets:
    path = os.path.join(NT_ATM, fname)
    content = open(path, encoding='utf-8').read()
    bracket_count = content.count('<Bracket>')
    sop_count = content.count('<StopStrategy>')
    entry_qty = re.search(r'<EntryQuantity>(\d+)</EntryQuantity>', content)
    total = entry_qty.group(1) if entry_qty else '?'
    status = '✅' if sop_count == bracket_count - 1 else f'❌ MISSING ({sop_count} SOP blocks, {bracket_count} brackets)'
    print(f'{status}  {fname:<30}  total={total}  brackets={bracket_count}  sop_blocks={sop_count}')
