"""
Update mes-atm-grid.html with new 2-target ATM values.

Strategy: regenerate the JavaScript data arrays that drive the HTML tables.
The HTML has JS objects/arrays defining the ATM config. We'll replace those sections.
"""

import re
from pathlib import Path

src = Path(r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html")
dst = Path(r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html")

html = src.read_text(encoding="utf-8")

# ─── New data tables ───────────────────────────────────────────────────────────

def t1_mes(sl): return round((1.5 + 0.5*(sl-4)) * 4)   # ticks
def t2_mes(sl): return sl * 2                             # = SL/2 pts * 4T/pt
def t1_mgc(sl): return round((1.5 + 0.5*(sl-4)) * 10)
def t2_mgc(sl): return sl * 5                             # = SL/2 pts * 10T/pt
def t1_pts(sl): return 1.5 + 0.5*(sl-4)
def t2_pts(sl): return sl / 2.0
def qty_split(total):
    if total % 2 == 0: return total//2, total//2
    return (total+1)//2, (total-1)//2

MES400 = {4:20,5:16,6:13,7:11,8:10,9:8,10:8}
MES200 = {4:10,5:8,6:7,7:6,8:5,9:5,10:4}
MES100 = {4:5,5:4,6:4,7:3,8:3,9:3,10:3}
MGC400 = {4:10,5:8,6:6,7:5,8:5,9:4,10:4}
MGC200 = {4:5,5:4,6:4,7:4,8:4,9:4,10:4}
MGC100 = {4:4,5:4,6:4,7:4,8:4,9:4,10:4}

SLS = list(range(4,11))

# Build new JS data object for the grid
# The HTML likely has a data structure like:
# const atmData = { mes400: [...], mes200: [...], mgc400: [...], mgc200: [...] }
# We need to find and replace these arrays.

# Let's inspect what pattern the HTML uses
# Search for the data patterns
print("Searching for data patterns in HTML...")

# Find where ATM data is defined
patterns = [
    r'const\s+atmData\s*=',
    r'const\s+gridData\s*=',
    r'const\s+mesData\s*=',
    r't1:\s*\{',
    r'T1.*ticks.*T2.*ticks',
    r'"sl4"',
    r'SL4.*8T',
    r'8T.*SL4',
]

for p in patterns:
    m = re.search(p, html, re.IGNORECASE)
    if m:
        print(f"Found '{p}' at pos {m.start()}: ...{html[m.start():m.start()+100]}...")
    else:
        print(f"NOT FOUND: '{p}'")
