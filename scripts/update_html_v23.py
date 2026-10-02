"""
Update mes-atm-grid.html: replace all ATM table rows with new 2-target values.
Uses regex substitutions targeted at the data cells.
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

SL_SOPs_MES = {4:"Scotty-SL4",5:"Scotty-SL5",6:"Scotty-SL6",7:"Scotty-SL7",
               8:"Scotty-SL8",9:"Scotty-SL9",10:"Scotty-SL10"}
SL_SOPs_MGC = {sl:f"MGC Scotty SL{sl}" for sl in range(4,11)}

src = Path(r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html")
html = src.read_text(encoding="utf-8")

def make_mes_row(acct, sl, totals_dict):
    total = totals_dict[sl]
    q1, q2 = qty_split(total)
    t1t = t1_mes(sl)
    t2t = t2_mes(sl)
    t1p = t1_pts(sl)
    t2p = t2_pts(sl)
    sop = SL_SOPs_MES[sl]
    max_risk = total * sl * 5  # contracts * sl_pts * $5/pt
    be_t = t1t

    risk_class = f"risk-{acct}"
    name = f"MES ${acct} SL{sl}"

    row = (
        f'    <tr>'
        f'<td><div class="atm-name">{name}</div>'
        f'<div class="atm-sub">{sop}</div></td>'
        f'<td><span class="sl-badge">{sl}pt / {sl*4}tk</span></td>'
        f'<td><span class="risk-badge {risk_class}">${max_risk}</span></td>'
        f'<td><span class="qty-badge">{total} cts</span></td>'
        f'<td><div class="target-cell">'
        f'<div class="t-label">T1 &ndash; {q1} cts</div>'
        f'<div class="t1-box">T1 = {t1p:.1f}pt ({t1t}tk)</div>'
        f'</div></td>'
        f'<td><div class="target-cell">'
        f'<div class="t-label">T2 &ndash; {q2} cts</div>'
        f'<div class="t2-box">T2 = {t2p:.1f}pt ({t2t}tk)</div>'
        f'</div></td>'
        f'<td><div class="target-cell">'
        f'<div class="t-label">&mdash;</div>'
        f'<div class="t3-box" style="opacity:0.35">removed</div>'
        f'</div></td>'
        f'<td><span class="be-badge">&#x1F551; {be_t}tk &rarr; +1tk</span></td>'
        f'<td><span class="sop-badge">{sop}</span></td>'
        f'</tr>\n'
    )
    return row

def make_mgc_row(acct, sl, totals_dict):
    total = totals_dict[sl]
    q1, q2 = qty_split(total)
    t1t = t1_mgc(sl)
    t2t = t2_mgc(sl)
    t1p = t1_pts(sl)
    t2p = t2_pts(sl)
    sop = SL_SOPs_MGC[sl]
    max_risk = total * sl * 10  # contracts * sl_pts * $10/pt
    be_t = t1t

    risk_class = f"risk-{acct}"
    name = f"MGC ${acct} SL{sl}"

    row = (
        f'    <tr>'
        f'<td><div class="atm-name">{name}</div>'
        f'<div class="atm-sub">{sop}</div></td>'
        f'<td><span class="sl-badge">{sl}pt / {sl*10}tk</span></td>'
        f'<td><span class="risk-badge {risk_class}">${max_risk}</span></td>'
        f'<td><span class="qty-badge">{total} cts</span></td>'
        f'<td><div class="target-cell">'
        f'<div class="t-label">T1 &ndash; {q1} cts</div>'
        f'<div class="t1-box">T1 = {t1p:.1f}pt ({t1t}tk)</div>'
        f'</div></td>'
        f'<td><div class="target-cell">'
        f'<div class="t-label">T2 &ndash; {q2} cts</div>'
        f'<div class="t2-box">T2 = {t2p:.1f}pt ({t2t}tk)</div>'
        f'</div></td>'
        f'<td><div class="target-cell">'
        f'<div class="t-label">&mdash;</div>'
        f'<div class="t3-box" style="opacity:0.35">removed</div>'
        f'</div></td>'
        f'<td><span class="be-badge">&#x1F551; {be_t}tk &rarr; +2tk</span></td>'
        f'<td><span class="sop-badge">{sop}</span></td>'
        f'</tr>\n'
    )
    return row

# Build new row blocks for each section
mes400_rows = "".join(make_mes_row(400, sl, MES400) for sl in range(4,11))
mes200_rows = "".join(make_mes_row(200, sl, MES200) for sl in range(4,11))
mes100_rows = "".join(make_mes_row(100, sl, MES100) for sl in range(4,11))
mgc400_rows = "".join(make_mgc_row(400, sl, MGC400) for sl in range(4,11))
mgc200_rows = "".join(make_mgc_row(200, sl, MGC200) for sl in range(4,11))
mgc100_rows = "".join(make_mgc_row(100, sl, MGC100) for sl in range(4,11))

# Replace tbody blocks for each instrument/account
# Pattern: find <tbody> ... </tbody> after the matching section header
def replace_tbody(html, section_marker, new_rows):
    # Find the section marker, then the next tbody block
    marker_pos = html.find(section_marker)
    if marker_pos == -1:
        print(f"WARNING: section marker not found: {section_marker!r}")
        return html
    tbody_start = html.find("<tbody>", marker_pos)
    tbody_end = html.find("</tbody>", tbody_start) + len("</tbody>")
    if tbody_start == -1 or tbody_end == -1:
        print(f"WARNING: tbody not found after marker: {section_marker!r}")
        return html
    replacement = f"<tbody>\n{new_rows}  </tbody>"
    return html[:tbody_start] + replacement + html[tbody_end:]

# Find section markers in the HTML
# Look for ATM CONFIG banners
print("Original HTML sections found:")
for marker in ["MES $400", "MES $200", "MES $100", "MGC $400", "MGC $200", "MGC $100"]:
    idx = html.find(f">{marker} SL4<")
    print(f"  '{marker} SL4': pos={idx}")

# Use the first occurrence of each instrument row to locate tbody
# Replace by finding each table's tbody section

# Strategy: find all <tbody> blocks and their surrounding context
tbodies = [(m.start(), m.end()) for m in re.finditer(r'<tbody>.*?</tbody>', html, re.DOTALL)]
print(f"\nFound {len(tbodies)} tbody blocks at positions:")
for i, (s, e) in enumerate(tbodies):
    # Find what ATM names are in this tbody
    content = html[s:min(s+200,e)]
    print(f"  [{i}] pos {s}: {content[:100].replace(chr(10),' ')}")
