import sys, re
sys.stdout.reconfigure(encoding='utf-8')

h1 = open(r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html', encoding='utf-8').read()
badges = re.findall(r'<span class="sop-badge">(.*?)</span>', h1)
print(f'Properly closed sop-badge spans: {len(badges)}')
print('Unique badge values:', sorted(set(badges)))
print()

h2 = open(r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html', encoding='utf-8').read()
lines = h2.split('\n')
print('Lines with >20T<, >15T<, >50T< in apex SOP html:')
for i, l in enumerate(lines, 1):
    if '>20T<' in l or '>15T<' in l or '>50T<' in l:
        print(f'  L{i}: {l.strip()[:150]}')
