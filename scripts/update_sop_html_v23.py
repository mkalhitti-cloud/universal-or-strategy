"""
Update apex-trading-sop.html with new 2-target ATM values.
Targets the comment blocks and displayed text that reference T1/T2/T3 values.
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
MGC400 = {4:10,5:8,6:6,7:5,8:5,9:4,10:4}
MGC200 = {4:5,5:4,6:4,7:4,8:4,9:4,10:4}

src = Path(r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html")
html = src.read_text(encoding="utf-8")

# Key replacements:

# 1. Update MES stop note header
html = html.replace(
    'Scotty "Kevinator" Stop Strategy \u2014 All MES ATMs SL4\u2013SL10',
    'Scotty Stop Strategy \u2014 All MES ATMs SL4\u2013SL10 | 2-Target Design'
)

# 2. Update MES stop strategy reference note
html = html.replace(
    "MES Stop Strategy Reference \u2014 Scotty-SL4 through Scotty-SL10 \u2013 T2 &amp; T3 brackets only",
    "MES Stop Strategy Reference \u2014 Scotty-SL4 through Scotty-SL10 \u2013 T2 bracket only (2-target)"
)

# 3. Replace old MES $400 comment block
old_mes400_comments = """        <!-- MES Stop 5pt \u2014 Q=16, T1q=8, T1=8T=2pt, T1gross=$80, T2=10T +1 slippage tick -->
        <!-- MES Stop 6pt \u2014 Q=13, T1q=6, T1=10T=2.5pt (+1 slip), T2q=7, T2=12T=3pt -->
        <!-- MES Stop 7pt \u2014 Q=11, T1q=5, T1=11T=2.75pt (+1 slip), T2q=6, T2=14T=3.5pt -->
        <!-- MES Stop 8pt \u2014 Q=10, T1q=5, T1=11T=2.75pt (+1 slip), T2q=5, T2=16T=4pt -->
        <!-- MES Stop 9pt \u2014 Q=8, T1q=4, T1=15T=3.75pt (+1 slip), T2q=4, T2=18T=4.5pt -->
        <!-- MES Stop 10pt \u2014 Q=8, T1q=4, T1=13T=3.25pt (+1 slip), T2q=4, T2=20T=5pt -->"""

new_mes400_comments = "\n".join(
    f"        <!-- MES $400 SL{sl}: Q={MES400[sl]}, T1q={qty_split(MES400[sl])[0]}, T1={t1_mes(sl)}T={t1_pts(sl):.1f}pt, T2q={qty_split(MES400[sl])[1]}, T2={t2_mes(sl)}T={t2_pts(sl):.1f}pt, BETrigger={t1_mes(sl)}T+1T -->"
    for sl in range(4,11)
)

if old_mes400_comments in html:
    html = html.replace(old_mes400_comments, new_mes400_comments)
    print("Replaced MES $400 comment block")
else:
    # Try partial match on first comment
    first_comment_pattern = r'<!-- MES Stop 5pt.*?-->'
    if re.search(first_comment_pattern, html, re.DOTALL):
        html = re.sub(
            r'<!-- MES Stop 5pt.*?<!-- MES Stop 10pt.*?-->',
            new_mes400_comments,
            html, flags=re.DOTALL
        )
        print("Replaced MES comment block via regex")
    else:
        print("WARNING: MES $400 comment block not found - skipping")

# 4. Update MES $200 comment block
old_mes200_comments = """        <!-- SL4: Q=10, single target 10 @ 8T=2pt -->
        <!-- SL5: Q=8, split 4/4, T1=8T, T2=10T -->
        <!-- SL6: Q=7, split 4/3, T1=10T, T2=12T (+1 approved 2026-08) -->
        <!-- SL7: Q=6, split 3/3, T1=11T, T2=14T (+1 approved 2026-08) -->
        <!-- SL8: Q=5, split 2/3, T1=11T, T2=16T -->
        <!-- SL9: Q=5, split 3/2, T1=15T, T2=18T (+1 approved 2026-08) -->
        <!-- SL10: Q=4, split 2/2, T1=13T, T2=20T -->"""

new_mes200_comments = "\n".join(
    f"        <!-- MES $200 SL{sl}: Q={MES200[sl]}, T1q={qty_split(MES200[sl])[0]}/{qty_split(MES200[sl])[1]}, T1={t1_mes(sl)}T={t1_pts(sl):.1f}pt, T2={t2_mes(sl)}T={t2_pts(sl):.1f}pt, BETrigger={t1_mes(sl)}T+1T -->"
    for sl in range(4,11)
)

if old_mes200_comments in html:
    html = html.replace(old_mes200_comments, new_mes200_comments)
    print("Replaced MES $200 comment block")
else:
    print("WARNING: MES $200 comment block not found - doing text search")
    # Find and print the actual comment block
    idx = html.find("SL4: Q=10, single target")
    if idx >= 0:
        print(f"  Found at {idx}: {html[idx:idx+300]}")

# 5. Update MGC $400 comment block
old_mgc400 = "<!-- SL4: Q=10, single @20T -->"
new_mgc400_comments = "\n".join(
    f"        <!-- MGC $400 SL{sl}: Q={MGC400[sl]}, T1q={qty_split(MGC400[sl])[0]}/{qty_split(MGC400[sl])[1]}, T1={t1_mgc(sl)}T={t1_pts(sl):.1f}pt, T2={t2_mgc(sl)}T={t2_pts(sl):.1f}pt, BETrigger={t1_mgc(sl)}T+2T -->"
    for sl in range(4,11)
)

# 6. Update the ATM files updated note
html = html.replace(
    "ATM files updated: MES $400/$200/$100 SL4\u2013SL10 (21 files). Stop strategy files: 7 files.",
    "ATM files updated: MES $400/$200/$100 + MGC $400/$200/$100 SL4\u2013SL10 (42 files). Stop strategy files: 14 files (Scotty-SL4\u2013SL10 + MGC Scotty SL4\u2013SL10). 2-target design."
)

# 7. Update BE buffer references
html = html.replace("BE=T1 ticks &rarr; +2tk", "BE=T1 ticks &rarr; +1tk (MES) / +2tk (MGC)")
html = html.replace("BE buffer = +0 ticks", "BE buffer = +1 tick (MES) / +2 ticks (MGC)")
html = html.replace("BE buffer = 0 (MES)", "BE buffer = 1T (MES) / 2T (MGC)")

dst = Path(r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html")
dst.write_text(html, encoding="utf-8")
print(f"\nSaved to {dst} ({len(html)} bytes)")
