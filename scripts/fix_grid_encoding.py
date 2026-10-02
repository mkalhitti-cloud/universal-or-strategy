"""Fix non-ASCII characters in mes-atm-grid.html"""
import re

path = r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html"

with open(path, encoding='utf-8') as f:
    text = f.read()

replacements = [
    ('\u2014', '&mdash;'),      # —
    ('\u2013', '&ndash;'),      # –
    ('\u2212', '&minus;'),      # − minus sign
    ('\u2192', '&rarr;'),       # →
    ('\u2550', '='),            # ═ box drawing
    ('\u00b7', '&middot;'),     # ·
    ('\u2022', '&bull;'),       # •
    ('\u2713', '&#10003;'),     # ✓
    ('\u2714', '&#10004;'),     # ✔
    ('\u2705', '&#9989;'),      # ✅
    ('\u274c', '&#10060;'),     # ❌
    ('\u25b6', '&#9654;'),      # ▶
    ('\u26a1', '&#9889;'),      # ⚡
    ('\u00a0', '&nbsp;'),
    ('\u00d7', '&times;'),
    ('\u00f7', '&divide;'),
    ('\u2265', '&ge;'),
    ('\u2264', '&le;'),
    ('\u2248', '&asymp;'),
    ('\u00b1', '&plusmn;'),
    ('\u23f1', '&#9201;'),      # ⏱
]

for char, entity in replacements:
    text = text.replace(char, entity)

def replace_high_unicode(m):
    return f'&#{ord(m.group(0))};'

text = re.sub(r'[^\x00-\xFF]', replace_high_unicode, text)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(text)

remaining = re.findall(r'[^\x00-\xFF]', text)
print(f"Done. Remaining non-ASCII: {len(remaining)}")
if remaining:
    print(set(remaining))
