"""Build ATM-Grid-v22.xlsx
Updates the MES ATM Grid $400 and MES ATM Grid $200 sheets with
Scotty Kevinator stop strategy values.
Copies all other tabs from v21 unchanged.
"""
import os, sys, math, copy
sys.stdout.reconfigure(encoding='utf-8')

try:
    import openpyxl
    from openpyxl import load_workbook
    from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
    from openpyxl.utils import get_column_letter
except ImportError:
    print("Installing openpyxl..."); os.system("pip install openpyxl -q")
    import openpyxl
    from openpyxl import load_workbook
    from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
    from openpyxl.utils import get_column_letter

SRC  = r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\ATM-Grid-v21.xlsx'
DEST = r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\ATM-Grid-v22.xlsx'

# ── SCOTTY LOGIC ──────────────────────────────────────────────────────────────
# Triggers: proportional to SL (50% / 83% / 100%)
# Trail-by stops: fixed 12T / 8T / 2T (Scotty's exact)
# BE trigger: 4T (T1=1pt), BE buffer: 0

def scotty_steps(sl_pts):
    sl_tk = sl_pts * 4
    s1t = round(sl_tk * 24 / 48)
    s2t = round(sl_tk * 40 / 48)
    s3t = sl_tk
    return s1t, s2t, s3t  # stops always 12, 8, 2

tick_val = 1.25
fee_rt   = 0.57

def qty(risk, sl): return int(risk / (sl * 5))
def split3(total):
    q1 = math.ceil(total / 2)
    q2 = math.ceil((total - q1) / 2)
    q3 = total - q1 - q2
    return q1, q2, q3
def ticks(pt): return int(round(pt * 4))
def pts_val(tk, q): return round(tk * tick_val * q, 2)
def fees(q): return round(fee_rt * q, 2)

# ── STYLES ────────────────────────────────────────────────────────────────────
HDR_FILL  = PatternFill('solid', fgColor='1F3864')
HDR2_FILL = PatternFill('solid', fgColor='2E75B6')
HDR3_FILL = PatternFill('solid', fgColor='305496')
ROW_FILL  = PatternFill('solid', fgColor='D9E1F2')
ROW2_FILL = PatternFill('solid', fgColor='EEF2F8')
GRN_FILL  = PatternFill('solid', fgColor='E2EFDA')
RED_FILL  = PatternFill('solid', fgColor='FCE4D6')
SOP_FILL  = PatternFill('solid', fgColor='F2F2F2')
WHT  = Font(color='FFFFFF', bold=True, size=10)
BLD  = Font(bold=True, size=10)
NRM  = Font(size=10)
SML  = Font(size=9, italic=True)

def bd():
    s = Side(style='thin')
    return Border(left=s, right=s, top=s, bottom=s)

CTR = Alignment(horizontal='center', vertical='center', wrap_text=True)
LFT = Alignment(horizontal='left',   vertical='center', wrap_text=True)

def hdr(ws, row, col, val, fill=HDR_FILL, font=WHT, align=CTR, span=1):
    c = ws.cell(row=row, column=col, value=val)
    c.fill = fill; c.font = font; c.alignment = align; c.border = bd()
    if span > 1:
        ws.merge_cells(start_row=row, start_column=col, end_row=row, end_column=col+span-1)
    return c

def cell(ws, row, col, val, fill=None, font=NRM, align=CTR):
    c = ws.cell(row=row, column=col, value=val)
    if fill: c.fill = fill
    c.font = font; c.alignment = align; c.border = bd()
    return c

# ── SHEET BUILDER ─────────────────────────────────────────────────────────────
def build_sheet(ws, risk):
    ws.sheet_view.showGridLines = False
    widths = [8,6,6,6,6,6,7,7,7,7,7,7,10,10,10,10,10,10,10,10]
    for i, w in enumerate(widths, 1):
        ws.column_dimensions[get_column_letter(i)].width = w
    ws.row_dimensions[1].height = 30
    ws.row_dimensions[2].height = 14

    # Title
    hdr(ws, 1, 1,
        f'MES ATM Grid ${risk}  |  Scotty "Kevinator" Stop Strategy  |  '
        f'T1=1pt(4T)  T2=50%SL  T3=75%SL  |  '
        f'BE=T1+0  Trail: triggers scaled to SL, stops fixed 12T/8T/2T  |  '
        f'Freq 4/2/1  |  $5/pt  $1.25/tick  fee=$0.57/contract',
        span=20)
    hdr(ws, 2, 1,
        'T1 qty=ceiling(total/2) · T2=ceiling(rest/2) · T3=remainder  |  '
        'Stop strategy: Scotty-SL4 through Scotty-SL10  |  '
        'Trigger=50%/83%/100% of SL · Stop-by=12T/8T/2T (fixed)',
        fill=HDR2_FILL, span=20)

    # ── SECTION 1: ATM CONFIG ─────────────────────────────────────────────────
    r = 4
    hdr(ws, r, 1, '▶  ATM CONFIG  —  Scotty Kevinator  SL4–SL10', fill=HDR3_FILL, span=20)
    r += 1
    cols = ['SL\n(pts)', 'SL\n(tk)', 'Total\nQty', 'T1\nQty', 'T2\nQty', 'T3\nQty',
            'T1\n(tk)', 'T2\n(tk)', 'T3\n(tk)',
            'BE\nTrigger', 'BE\nBuffer',
            'S1\nTrigger', 'S1\nStop', 'S1\nFreq',
            'S2\nTrigger', 'S2\nStop', 'S2\nFreq',
            'S3\nTrigger', 'S3\nStop', 'S3\nFreq']
    for ci, col in enumerate(cols, 1):
        hdr(ws, r, ci, col, fill=HDR2_FILL)
    r += 1

    for sl_pts in range(4, 11):
        sl_tk = sl_pts * 4
        total = qty(risk, sl_pts)
        q1, q2, q3 = split3(total)
        t1, t2, t3 = 4, sl_tk // 2, int(sl_tk * 0.75)
        s1t, s2t, s3t = scotty_steps(sl_pts)
        fill = ROW_FILL if sl_pts % 2 == 0 else ROW2_FILL
        vals = [sl_pts, sl_tk, total, q1, q2, q3,
                t1, t2, t3,
                '4T', '+0',
                f'{s1t}T', '12T', '4',
                f'{s2t}T', '8T',  '2',
                f'{s3t}T', '2T',  '1']
        for ci, v in enumerate(vals, 1):
            cell(ws, r, ci, v, fill=fill)
        r += 1

    # ── SECTION 2: SCENARIO PnL ───────────────────────────────────────────────
    r += 1
    hdr(ws, r, 1, '▶  SCENARIO PnL  —  per SL  (Scotty Kevinator trail)', fill=HDR3_FILL, span=20)
    r += 1
    scen_cols = ['SL', 'SOP', 'Total\nQty', 'T1\nfill', 'T2\nfull', 'T3\nfull',
                 'T2 S1\nstop', 'T2 S2\nstop', 'T2 S3\nstop',
                 'Hard\nStop', 'Fees']
    for ci, col in enumerate(scen_cols, 1):
        hdr(ws, r, ci, col, fill=HDR2_FILL)
    r += 1

    for sl_pts in range(4, 11):
        sl_tk = sl_pts * 4
        total = qty(risk, sl_pts)
        q1, q2, q3 = split3(total)
        t1, t2, t3 = 4, sl_tk // 2, int(sl_tk * 0.75)
        s1t, s2t, s3t = scotty_steps(sl_pts)
        fee = fees(total)
        fill = ROW_FILL if sl_pts % 2 == 0 else ROW2_FILL

        t1_pnl  = round(pts_val(t1,  q1) + pts_val(t2, q2)  + pts_val(t3, q3)  - fee, 2)
        t2_full = round(pts_val(t1,  q1) + pts_val(t2, q2)  + pts_val(t3, q3)  - fee, 2)
        t2_s1   = round(pts_val(t1,  q1) + pts_val(s1t-12, q2+q3) - fee, 2)  # stopped at floor after S1
        t2_s2   = round(pts_val(t1,  q1) + pts_val(s2t-8,  q2+q3) - fee, 2)
        t2_s3   = round(pts_val(t1,  q1) + pts_val(s3t-2,  q2+q3) - fee, 2)
        hard    = round(-pts_val(sl_tk, total) - fee, 2)

        vals = [f'SL{sl_pts}', f'Scotty-SL{sl_pts}', total,
                f'+${pts_val(t1,q1):.2f}',
                f'+${t2_full:.2f}',
                f'+${t2_full:.2f}',
                f'${t2_s1:+.2f}', f'${t2_s2:+.2f}', f'${t2_s3:+.2f}',
                f'${hard:.2f}', f'${fee:.2f}']
        fills = [fill, fill, fill,
                 GRN_FILL, GRN_FILL, GRN_FILL,
                 GRN_FILL, GRN_FILL, GRN_FILL,
                 RED_FILL, fill]
        for ci, (v, f_) in enumerate(zip(vals, fills), 1):
            cell(ws, r, ci, v, fill=f_)
        r += 1

    # ── SECTION 3: STOP STRATEGY REFERENCE ───────────────────────────────────
    r += 1
    hdr(ws, r, 1,
        '▶  SCOTTY KEVINATOR STOP STRATEGY REFERENCE  —  Triggers scale with SL · Stops fixed 12T/8T/2T',
        fill=HDR3_FILL, span=20)
    r += 1
    ref_cols = ['Template', 'ATMs', 'SL ticks', 'BE trigger', 'BE buffer',
                'S1 trigger', 'S1 stop (trail-by)', 'S1 freq',
                'S2 trigger', 'S2 stop (trail-by)', 'S2 freq',
                'S3 trigger', 'S3 stop (trail-by)', 'S3 freq', 'Notes']
    for ci, col in enumerate(ref_cols, 1):
        hdr(ws, r, ci, col, fill=HDR2_FILL)
    r += 1

    for i, sl_pts in enumerate(range(4, 11)):
        sl_tk = sl_pts * 4
        s1t, s2t, s3t = scotty_steps(sl_pts)
        fill = ROW_FILL if i % 2 == 0 else ROW2_FILL
        vals = [f'Scotty-SL{sl_pts}',
                f'MES $400/$200/$100 SL{sl_pts}',
                sl_tk, '4T (T1=1pt)', '+0',
                f'{s1t}T (50% SL)', '12T', '4',
                f'{s2t}T (83% SL)', '8T',  '2',
                f'{s3t}T (100% SL)', '2T', '1',
                f'Floor after S3: {s3t-2}T from entry']
        for ci, v in enumerate(vals, 1):
            cell(ws, r, ci, v, fill=fill)
        r += 1

    # ── SECTION 4: NOTES ─────────────────────────────────────────────────────
    r += 1
    hdr(ws, r, 1, '▶  DESIGN NOTES', fill=HDR3_FILL, span=20)
    r += 1
    notes = [
        'Source: Scotty (WSGTA) "The Kevinator" stop strategy — adapted for MES SL4-SL10',
        'Triggers proportional to SL: Step 1 fires at 50% SL (= T2 target), Step 2 at 83% SL, Step 3 at 100% SL',
        'Trail-by stops are FIXED (12T / 8T / 2T) — Scotty\'s exact values, same for all SL sizes',
        'BE buffer = 0 (MES room to move). BE fires when T1 fills (4T = 1pt). Stop moves to entry flat.',
        'T1 bracket: NO stop strategy — just fills at 1pt target. T2 and T3 carry the Scotty trail.',
        'Frequency 4 on Step 1 (aggressive early protection after 50% SL earned), 2 and 1 on Steps 2/3',
        '7 stop strategy XML files: Scotty-SL4.xml through Scotty-SL10.xml in NinjaTrader StopStrategy folder',
        '21 ATM XML files updated: MES $400/$200/$100 SL4-SL10 in NinjaTrader AtmStrategy folder',
    ]
    for note in notes:
        c = ws.cell(row=r, column=1, value=note)
        c.font = SML; c.alignment = LFT; c.fill = SOP_FILL
        ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=20)
        r += 1

    ws.freeze_panes = 'A4'

# ── LOAD SRC, REPLACE SHEETS, SAVE ───────────────────────────────────────────
wb = load_workbook(SRC)

for tab_name in ['MES ATM Grid $400', 'MES ATM Grid $200']:
    risk = int(tab_name.split('$')[1])
    if tab_name in wb.sheetnames:
        idx = wb.sheetnames.index(tab_name)
        del wb[tab_name]
    else:
        idx = 0
    wb.create_sheet(tab_name, idx)
    build_sheet(wb[tab_name], risk)
    print(f'  OK  {tab_name}')

wb.save(DEST)
print(f'\nSaved: {DEST}')
print(f'Sheets: {wb.sheetnames}')
