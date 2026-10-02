"""
Update mes-atm-grid.html: replace ATM table rows with new 2-target values.
"""

import re
from pathlib import Path

def t1_mes(sl): return round((1.5 + 0.5*(sl-4)) * 4)
def t2_mes(sl): return sl * 2
def t1_mgc(sl): return round((1.5 + 0.5*(sl-4)) * 10)
def t2_mgc(sl): return sl * 5
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

SL_SOPs_MES = {sl:f"Scotty-SL{sl}" for sl in range(4,11)}
SL_SOPs_MGC = {sl:f"MGC Scotty SL{sl}" for sl in range(4,11)}

src = Path(r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html")
html = src.read_text(encoding="utf-8")

def make_row(instrument, acct, sl, totals_dict):
    total = totals_dict[sl]
    q1, q2 = qty_split(total)
    if instrument == "MES":
        t1t, t2t = t1_mes(sl), t2_mes(sl)
        ticks_per_pt, dollars_per_pt = 4, 5
        be_buf = "+1tk"
        sop = SL_SOPs_MES[sl]
    else:
        t1t, t2t = t1_mgc(sl), t2_mgc(sl)
        ticks_per_pt, dollars_per_pt = 10, 10
        be_buf = "+2tk"
        sop = SL_SOPs_MGC[sl]

    t1p = t1_pts(sl)
    t2p = t2_pts(sl)
    max_risk = total * sl * dollars_per_pt
    risk_class = f"risk-{acct}"
    name = f"{instrument} ${acct} SL{sl}"
    sl_tk = sl * ticks_per_pt

    return (
        f'    <tr>'
        f'<td><div class="atm-name">{name}</div><div class="atm-sub">{sop}</div></td>'
        f'<td><span class="sl-badge">{sl}pt / {sl_tk}tk</span></td>'
        f'<td><span class="risk-badge {risk_class}">${max_risk}</span></td>'
        f'<td><span class="qty-badge">{total} cts</span></td>'
        f'<td><div class="target-cell"><div class="t-label">T1 &ndash; {q1} cts</div>'
        f'<div class="t1-box">T1 = {t1p:.1f}pt ({t1t}tk)</div></div></td>'
        f'<td><div class="target-cell"><div class="t-label">T2 &ndash; {q2} cts</div>'
        f'<div class="t2-box">T2 = {t2p:.1f}pt ({t2t}tk)</div></div></td>'
        f'<td><div class="target-cell"><div class="t-label">&mdash;</div>'
        f'<div class="t3-box" style="opacity:0.35">removed</div></div></td>'
        f'<td><span class="be-badge">&#x23F1; {t1t}tk &rarr; {be_buf}</span></td>'
        f'<td><span class="sop-badge">{sop}</span></td>'
        f'</tr>\n'
    )

# Build row sets
row_sets = [
    "".join(make_row("MES",400,sl,MES400) for sl in range(4,11)),
    "".join(make_row("MES",200,sl,MES200) for sl in range(4,11)),
    "".join(make_row("MES",100,sl,MES100) for sl in range(4,11)),
    "".join(make_row("MGC",400,sl,MGC400) for sl in range(4,11)),
    "".join(make_row("MGC",200,sl,MGC200) for sl in range(4,11)),
    "".join(make_row("MGC",100,sl,MGC100) for sl in range(4,11)),
]

# Find all tbody blocks
tbodies = [(m.start(), m.end()) for m in re.finditer(r'<tbody>.*?</tbody>', html, re.DOTALL)]

# ATM config tbodies are at indices: 0=MES400, 2=MES200, 4=MES100, 5=MGC400, 6=MGC200, 7=MGC100
TARGET_INDICES = [0, 2, 4, 5, 6, 7]

# Replace from back to front to preserve positions
replacements = list(zip(TARGET_INDICES, row_sets))
replacements.sort(key=lambda x: -tbodies[x[0]][0])  # sort by pos descending

for tbody_idx, new_rows in replacements:
    s, e = tbodies[tbody_idx]
    replacement = f"<tbody>\n{new_rows}  </tbody>"
    html = html[:s] + replacement + html[e:]
    print(f"Replaced tbody[{tbody_idx}] at pos {s}")

# Update header descriptions
html = html.replace(
    "Micro E-mini S&amp;P 500 &amp; Micro Gold \u2014 NinjaTrader ATM Reference \u2014 3-Target + SOP Trail Design",
    "Micro E-mini S&amp;P 500 &amp; Micro Gold \u2014 NinjaTrader ATM Reference \u2014 2-Target Design"
)
# Update BE buffer note in header
html = re.sub(r'T1=\d+tk\(\d+pt\)', lambda m: m.group().replace("T1=4tk(1pt)", "T1=6-18tk(1.5-4.5pt)"), html)

# Update page subtitle/spec line
old_spec = r'T1=4tk\(1pt\)\s*&amp;[^<]*T1=10tk\(1pt\)'
html = re.sub(old_spec,
    "T1=SL4:6tk..SL10:18tk &nbsp;|&nbsp; <span>MGC:</span> $1/tick &mdash; 10tk/pt &mdash; T1=SL4:15tk..SL10:45tk",
    html)

# Update section banners
html = html.replace(
    "ATM CONFIG \u2014 SL4\u2013SL10 \u2013 T1=1pt(4tk fixed) \u2013 T2=50%SL \u2013 T3=75%SL \u2013 SOP trail on Q2+Q3",
    "ATM CONFIG \u2014 SL4\u2013SL10 \u2013 T1=1.5pt+(SL-4)*0.5pt \u2013 T2=50%SL \u2013 2 targets \u2013 Scotty trail on T2"
)
html = html.replace(
    "ATM CONFIG \u2014 Same targets as $400 \u2013 T1=1pt(4tk) \u2013 T2=50%SL \u2013 T3=75%SL",
    "ATM CONFIG \u2014 Same targets as $400 \u2013 2-target design"
)

dst = Path(r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html")
dst.write_text(html, encoding="utf-8")
print(f"\nSaved to {dst} ({len(html)} bytes)")
