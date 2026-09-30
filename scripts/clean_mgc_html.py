import sys
sys.stdout.reconfigure(encoding='utf-8')

path = r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html'
content = open(path, encoding='utf-8').read()

replacements = [
    ('MGC SOP3 \u00b7 45% usage', 'Scotty SOP \u00b7 45% usage'),
    ('MGC SOP3 \u00b7 30% usage', 'Scotty SOP \u00b7 30% usage'),
    ('MGC SOP3 \u00b7 15% usage', 'Scotty SOP \u00b7 15% usage'),
    ('MGC SOP35',                 'Scotty SOP'),
    ('MGC SOP45',                 'Scotty SOP'),
    ('MGC SOP4',                  'Scotty SOP'),
    ('MGC SOP5',                  'Scotty SOP'),
    ('MGC SOP3',                  'Scotty SOP'),
]

for old, new in replacements:
    n = content.count(old)
    if n:
        content = content.replace(old, new)
        print(f'  replaced {n}x  "{old}" -> "{new}"')

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

old_names = ['MGC SOP3', 'MGC SOP35', 'MGC SOP4', 'MGC SOP45', 'MGC SOP5']
remaining = [x for x in old_names if x in content]
scotty_count = content.count('Scotty SOP')
print()
print(f'Scotty SOP occurrences: {scotty_count}')
if remaining:
    print('STILL PRESENT:', remaining)
else:
    print('HTML CLEAN - no old MGC SOP references remain')
