import json

with open("scripts/xlsx_ops_v23.json") as f:
    ops = json.load(f)

# Remove wrong MES $100 ops (wrong row offset, wrong columns)
ops = [o for o in ops if "MES ATM Grid $100" not in o.get("path","")]
print(f"After removing bad MES100 ops: {len(ops)}")

SL_LIST = [4,5,6,7,8,9,10]
def t1_mes(sl): return round((1.5 + 0.5*(sl-4)) * 4)
def t2_mes(sl): return sl * 2
def qty_split(total):
    if total % 2 == 0: return total//2, total//2
    return (total+1)//2, (total-1)//2
MES100 = {4:5, 5:4, 6:4, 7:3, 8:3, 9:3, 10:3}

sheet = "MES ATM Grid $100"
for i, sl in enumerate(SL_LIST):
    row = 6 + i   # data rows 6-12
    t1t = t1_mes(sl)
    t2t = t2_mes(sl)
    t1p = 1.5 + 0.5*(sl-4)
    t2p = sl / 2.0
    total = MES100[sl]
    q1, q2 = qty_split(total)
    ops += [
        {"command":"set","path":f"/{sheet}/E{row}","props":{"value":q1}},
        {"command":"set","path":f"/{sheet}/F{row}","props":{"value":q2}},
        {"command":"set","path":f"/{sheet}/G{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/H{row}","props":{"value":f"{t1p:.1f}pt"}},
        {"command":"set","path":f"/{sheet}/I{row}","props":{"value":t1t}},
        {"command":"set","path":f"/{sheet}/J{row}","props":{"value":f"{t2p:.2f}pt"}},
        {"command":"set","path":f"/{sheet}/K{row}","props":{"value":t2t}},
        {"command":"set","path":f"/{sheet}/L{row}","props":{"value":"-"}},
        {"command":"set","path":f"/{sheet}/M{row}","props":{"value":"-"}},
    ]

print(f"Final ops: {len(ops)}")
with open("scripts/xlsx_ops_v23.json","w") as f:
    json.dump(ops, f, indent=2)
print("Updated xlsx_ops_v23.json")
