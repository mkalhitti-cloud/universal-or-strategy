"""
Build the office_edit batch JSON for ATM-Grid-v22.xlsx updates.

Changes per approved transformation:
- T1 ticks: SL4=6T,SL5=8T,SL6=10T,SL7=12T,SL8=14T,SL9=16T,SL10=18T  (MES 4T/pt)
- T2 ticks: SL4=8T,SL5=10T,SL6=12T,SL7=14T,SL8=16T,SL9=18T,SL10=20T  (MES, = SL/2 pts)
- T3 removed (2-target design) — T3 cols set to blank / "-"
- T1 qty / T2 qty = even split of total, T1 gets extra on odd
- BE trigger = T1 ticks (was 4T for all)
- BE buffer = 1 for MES (was 0); 2 for MGC (unchanged)
- MGC T1 ticks: SL4=15T,SL5=20T,SL6=25T,SL7=30T,SL8=35T,SL9=40T,SL10=45T
- MGC T2 ticks: SL4=20T,SL5=25T,SL6=30T,SL7=35T,SL8=40T,SL9=45T,SL10=50T
"""

import json

# Data tables
SL_LIST = [4,5,6,7,8,9,10]

def t1_mes(sl): return round((1.5 + 0.5*(sl-4)) * 4)   # ticks
def t2_mes(sl): return sl * 2                             # ticks = SL pts * 4T/pt / 2... wait
# T2 = SL/2 pts, MES 4T/pt => T2 = SL*4/2 = SL*2
def t1_mgc(sl): return round((1.5 + 0.5*(sl-4)) * 10)
def t2_mgc(sl): return sl * 5   # T2 = SL/2 pts * 10T/pt

def qty_split(total):
    if total % 2 == 0: return total//2, total//2
    return (total+1)//2, (total-1)//2

MES400 = {4:20,5:16,6:13,7:11,8:10,9:8,10:8}
MES200 = {4:10,5:8,6:7,7:6,8:5,9:5,10:4}
MES100 = {4:5,5:4,6:4,7:3,8:3,9:3,10:3}
MGC400 = {4:10,5:8,6:6,7:5,8:5,9:4,10:4}
MGC200 = {4:5,5:4,6:4,7:4,8:4,9:4,10:4}
MGC100 = {4:4,5:4,6:4,7:4,8:4,9:4,10:4}

ops = []
row_offset = 6   # data rows start at row 6

# ─── MES $400 ATM Grid ───────────────────────────────────────────────────────
# Columns: A=SL, B=SL ticks, C=Total, D=T1qty, E=T2qty, F=T3qty, G=T1ticks, H=T2ticks, I=T3ticks
#          J=BE trigger, K=BE buffer
sheet = "MES ATM Grid $400"
for i, sl in enumerate(SL_LIST):
    row = row_offset + i
    t1t, t2t = t1_mes(sl), t2_mes(sl)
    total = MES400[sl]
    q1, q2 = qty_split(total)
    be_trig = f"{t1t}T"
    ops += [
        {"command":"set","path":f"/{sheet}/D{row}","props":{"value":q1}},
        {"command":"set","path":f"/{sheet}/E{row}","props":{"value":q2}},
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/G{row}","props":{"value":t1t}},
        {"command":"set","path":f"/{sheet}/H{row}","props":{"value":t2t}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/J{row}","props":{"value":be_trig}},
        {"command":"set","path":f"/{sheet}/K{row}","props":{"value":"+1"}},
    ]
# Update header row 1 description
ops.append({"command":"set","path":f"/{sheet}/A1","props":{
    "value":"MES ATM Grid $400  |  Scotty Stop Strategy  |  T1=1.5pt+(SL-4)*0.5pt  T2=50%SL  |  BE=T1+1T  Trail: triggers scaled to SL, stops fixed 12T/8T/2T  |  Freq 4/2/1  |  $5/pt  $1.25/tick  fee=$0.57/contract"
}})
ops.append({"command":"set","path":f"/{sheet}/A2","props":{
    "value":"T1 qty=ceil(total/2) >= T2 qty=floor(total/2)  |  2-target design (T3 removed)  |  Stop strategy: Scotty-SL4 through Scotty-SL10  |  BE trigger=T1 ticks, BE buffer=+1T"
}})
# Update Scotty reference rows 26-32 (BE trigger and buffer)
for i, sl in enumerate(SL_LIST):
    row = 26 + i
    t1t = t1_mes(sl)
    ops.append({"command":"set","path":f"/{sheet}/D{row}","props":{"value":f"{t1t}T (T1={1.5+0.5*(sl-4):.1f}pt)"}})
    ops.append({"command":"set","path":f"/{sheet}/E{row}","props":{"value":"+1"}})

# ─── MES $200 ATM Grid ───────────────────────────────────────────────────────
sheet = "MES ATM Grid $200"
for i, sl in enumerate(SL_LIST):
    row = row_offset + i
    t1t, t2t = t1_mes(sl), t2_mes(sl)
    total = MES200[sl]
    q1, q2 = qty_split(total)
    be_trig = f"{t1t}T"
    ops += [
        {"command":"set","path":f"/{sheet}/D{row}","props":{"value":q1}},
        {"command":"set","path":f"/{sheet}/E{row}","props":{"value":q2}},
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/G{row}","props":{"value":t1t}},
        {"command":"set","path":f"/{sheet}/H{row}","props":{"value":t2t}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/J{row}","props":{"value":be_trig}},
        {"command":"set","path":f"/{sheet}/K{row}","props":{"value":"+1"}},
    ]
ops.append({"command":"set","path":f"/{sheet}/A1","props":{
    "value":"MES ATM Grid $200  |  Scotty Stop Strategy  |  T1=1.5pt+(SL-4)*0.5pt  T2=50%SL  |  BE=T1+1T  Trail: triggers scaled to SL, stops fixed 12T/8T/2T  |  Freq 4/2/1  |  $5/pt  $1.25/tick  fee=$0.57/contract"
}})
ops.append({"command":"set","path":f"/{sheet}/A2","props":{
    "value":"T1 qty=ceil(total/2) >= T2 qty=floor(total/2)  |  2-target design (T3 removed)  |  Stop strategy: Scotty-SL4 through Scotty-SL10  |  BE trigger=T1 ticks, BE buffer=+1T"
}})
for i, sl in enumerate(SL_LIST):
    row = 26 + i
    t1t = t1_mes(sl)
    ops.append({"command":"set","path":f"/{sheet}/D{row}","props":{"value":f"{t1t}T (T1={1.5+0.5*(sl-4):.1f}pt)"}})
    ops.append({"command":"set","path":f"/{sheet}/E{row}","props":{"value":"+1"}})

# ─── MES $100 ATM Grid ───────────────────────────────────────────────────────
# Rows 4-10 (7 SLs), check structure: similar but smaller
# From dump rows 4-10 (row[4]=SL4..row[10]=SL10)
sheet = "MES ATM Grid $100"
# MES $100 has rows 4-10 for data based on the dump (11 rows total, headers at row ~2-3)
# Let me use row_offset=4 for this sheet
mes100_row_offset = 4
for i, sl in enumerate(SL_LIST):
    row = mes100_row_offset + i
    t1t, t2t = t1_mes(sl), t2_mes(sl)
    total = MES100[sl]
    q1, q2 = qty_split(total)
    be_trig = f"{t1t}T"
    ops += [
        {"command":"set","path":f"/{sheet}/D{row}","props":{"value":q1}},
        {"command":"set","path":f"/{sheet}/E{row}","props":{"value":q2}},
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/G{row}","props":{"value":t1t}},
        {"command":"set","path":f"/{sheet}/H{row}","props":{"value":t2t}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/J{row}","props":{"value":be_trig}},
        {"command":"set","path":f"/{sheet}/K{row}","props":{"value":"+1"}},
    ]

# ─── MGC $400 ATM Grid ───────────────────────────────────────────────────────
# From dump: rows 6-12 for data. Columns: A=Stop, B=Qty, C=ActMax, D=T1qty, E=T1ticks, F=T1pts, G=T1gross
#            H=blank, I=T2qty, J=T2ticks, K=T2pts, L=T2gross, ...
sheet = "MGC ATM Grid $400"
for i, sl in enumerate(SL_LIST):
    row = row_offset + i
    t1t = t1_mgc(sl)
    t2t = t2_mgc(sl)
    t1p = 1.5 + 0.5*(sl-4)
    t2p = sl/2.0
    total = MGC400[sl]
    q1, q2 = qty_split(total)
    ops += [
        {"command":"set","path":f"/{sheet}/D{row}","props":{"value":q1}},
        {"command":"set","path":f"/{sheet}/E{row}","props":{"value":t1t}},
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":f"{t1p:.2f}pt"}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":q2}},
        {"command":"set","path":f"/{sheet}/J{row}","props":{"value":t2t}},
        {"command":"set","path":f"/{sheet}/K{row}","props":{"value":f"{t2p:.2f}pt"}},
    ]
# Reference table rows 16-22
ref_row_offset = 16
for i, sl in enumerate(SL_LIST):
    row = ref_row_offset + i
    t1t = t1_mgc(sl)
    t2t = t2_mgc(sl)
    total = MGC400[sl]
    q1, q2 = qty_split(total)
    ops += [
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":f"{q1}/{q2}"}},
        {"command":"set","path":f"/{sheet}/G{row}","props":{"value":t1t}},
        {"command":"set","path":f"/{sheet}/H{row}","props":{"value":t2t}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":f"{t2t}T ({sl/2:.2f}pt)"}},
    ]
ops.append({"command":"set","path":f"/{sheet}/A1","props":{
    "value":"MGC ATM Design Grid  |  T1=1.5pt+(SL-4)*0.5pt  T2=50%SL  |  $10/pt  |  $1/tick  |  Fee~$1.5/contract  |  BE trigger=T1 fill (+2tk buffer)"
}})

# ─── MGC $200 ATM Grid ───────────────────────────────────────────────────────
sheet = "MGC ATM Grid $200"
for i, sl in enumerate(SL_LIST):
    row = row_offset + i
    t1t = t1_mgc(sl)
    t2t = t2_mgc(sl)
    t1p = 1.5 + 0.5*(sl-4)
    t2p = sl/2.0
    total = MGC200[sl]
    q1, q2 = qty_split(total)
    ops += [
        {"command":"set","path":f"/{sheet}/D{row}","props":{"value":q1}},
        {"command":"set","path":f"/{sheet}/E{row}","props":{"value":t1t}},
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":f"{t1p:.2f}pt"}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":q2}},
        {"command":"set","path":f"/{sheet}/J{row}","props":{"value":t2t}},
        {"command":"set","path":f"/{sheet}/K{row}","props":{"value":f"{t2p:.2f}pt"}},
    ]

# ─── Morning Card sheet ───────────────────────────────────────────────────────
sheet = "📋 Morning Card"
# Rows 5-11 = SL4-SL10
# Cols: A=MES$400 label, B=MES$400 T1ticks, C=MES$400 net(skip),
#       E=MES$200 label, F=MES$200 T1ticks,
#       I=MGC$400 label, J=MGC$400 T1ticks,
#       M=MGC$200 label, N=MGC$200 T1ticks, Q=Notes
mc_rows = {4:5,5:6,6:7,7:8,8:9,9:10,10:11}
for sl, row in mc_rows.items():
    t1m = t1_mes(sl)
    t1g = t1_mgc(sl)
    q400 = MES400[sl]; q1_400, q2_400 = qty_split(q400)
    q200 = MES200[sl]; q1_200, q2_200 = qty_split(q200)
    gq400 = MGC400[sl]; gq1_400, gq2_400 = qty_split(gq400)
    gq200 = MGC200[sl]; gq1_200, gq2_200 = qty_split(gq200)
    ops += [
        {"command":"set","path":f"/{sheet}/A{row}","props":{"value":f"SL{sl} Qty={q400}"}},
        {"command":"set","path":f"/{sheet}/B{row}","props":{"value":f"{t1m}T"}},
        {"command":"set","path":f"/{sheet}/E{row}","props":{"value":f"SL{sl} Qty={q200}"}},
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":f"{t1m}T"}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":f"SL{sl} Qty={gq400}"}},
        {"command":"set","path":f"/{sheet}/J{row}","props":{"value":f"{t1g}T"}},
        {"command":"set","path":f"/{sheet}/M{row}","props":{"value":f"SL{sl} Qty={gq200}"}},
        {"command":"set","path":f"/{sheet}/N{row}","props":{"value":f"{t1g}T"}},
    ]
# Update morning card header row 2 (BE note)
ops.append({"command":"set","path":f"/{sheet}/A2","props":{
    "value":f"MES: $1.25/tick | T1 ticks per SL: SL4=6T..SL10=18T | BE+1T | MGC: $1.00/tick | T1 ticks: SL4=15T..SL10=45T | BE+2T | 2-target design"
}})
ops.append({"command":"set","path":f"/{sheet}/A13","props":{
    "value":"BE STOP RULE:  MES = BE+1T (trigger = T1)  |  MGC = BE+2T (trigger = T1)  |  Stop moves to entry+offset the moment T1 fills"
}})
ops.append({"command":"set","path":f"/{sheet}/A14","props":{
    "value":"T1 qty >= T2 qty (CEIL split). T1 ticks: SL4=6T(1.5pt) to SL10=18T(4.5pt). T2 = 50% SL. 2-target design, no T3."
}})

print(f"Total ops: {len(ops)}")
print(json.dumps(ops, indent=2)[:500], "...")
# Save to file
with open("scripts/xlsx_ops_v23.json","w") as f:
    json.dump(ops, f, indent=2)
print("Saved to scripts/xlsx_ops_v23.json")
