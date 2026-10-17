"""
Fix T1 StopStrategy in all MES and MGC NinjaTrader ATM XML files.
Rewrites the <Brackets> section cleanly with T1 carrying the same StopStrategy as T2.
"""
import os
import re

NT_PATH = os.path.expandvars(r"%USERPROFILE%\Documents\NinjaTrader 8\templates\AtmStrategy")

# ── MES config (from verified audit data) ──────────────────────────────────────
# Keys: (instrument_tier, sl) -> {q1, q2, sl_ticks, t1_ticks, t2_ticks, entry_qty,
#        be_plus, be_trigger, steps: [(freq,pt,stop),...], tpl, atm_selector}

# SL -> stop strategy config (same across $400/$200/$100, only qtys differ)
MES_SOP = {
    4:  dict(be_plus=1, be_trigger=8, steps=[(4,8,12),(2,13,8),(1,16,2)],  tpl="Scotty-SL4"),
    5:  dict(be_plus=1, be_trigger=8, steps=[(4,10,12),(2,17,8),(1,20,2)], tpl="Scotty-SL5"),
    6:  dict(be_plus=1, be_trigger=8, steps=[(4,12,12),(2,20,8),(1,24,2)], tpl="Scotty-SL6"),
    7:  dict(be_plus=1, be_trigger=8, steps=[(4,14,12),(2,23,8),(1,28,2)], tpl="Scotty-SL7"),
    8:  dict(be_plus=1, be_trigger=8, steps=[(4,16,12),(2,27,8),(1,32,2)], tpl="Scotty-SL8"),
    9:  dict(be_plus=1, be_trigger=8, steps=[(4,18,12),(2,30,8),(1,36,2)], tpl="Scotty-SL9"),
    10: dict(be_plus=1, be_trigger=8, steps=[(4,20,12),(2,33,8),(1,40,2)], tpl="Scotty-SL10"),
}

# MGC: universal SOP for all SLs, only template name differs
MGC_SOP = {
    sl: dict(be_plus=2, be_trigger=20, steps=[(2,20,5),(2,30,5),(1,40,2)], tpl=f"MGC Scotty SL{sl}")
    for sl in range(4, 11)
}

# Full bracket data from audit (verified before any changes):
# (fname_key) -> (q1, sl_ticks, t1_ticks, q2, t2_ticks, entry_qty)
MES_DATA = {
    # MES $400
    "MES $400 SL4":  (10, 16, 8,  10, 12, 20),
    "MES $400 SL5":  (8,  20, 8,  8,  12, 16),
    "MES $400 SL6":  (7,  24, 8,  6,  12, 13),
    "MES $400 SL7":  (6,  28, 8,  5,  14, 11),
    "MES $400 SL8":  (5,  32, 8,  5,  16, 10),
    "MES $400 SL9":  (4,  36, 8,  4,  18, 8),
    "MES $400 SL10": (4,  40, 8,  4,  20, 8),
    # MES $200
    "MES $200 SL4":  (5,  16, 8,  5,  12, 10),
    "MES $200 SL5":  (4,  20, 8,  4,  12, 8),
    "MES $200 SL6":  (4,  24, 8,  3,  12, 7),
    "MES $200 SL7":  (3,  28, 8,  3,  14, 6),
    "MES $200 SL8":  (3,  32, 8,  2,  16, 5),
    "MES $200 SL9":  (3,  36, 8,  2,  18, 5),
    "MES $200 SL10": (2,  40, 8,  2,  20, 4),
    # MES $100
    "MES $100 SL4":  (3,  16, 8,  2,  12, 5),
    "MES $100 SL5":  (2,  20, 8,  2,  12, 4),
    "MES $100 SL6":  (2,  24, 8,  2,  12, 4),
    "MES $100 SL7":  (2,  28, 8,  1,  14, 3),
    "MES $100 SL8":  (2,  32, 8,  1,  16, 3),
    "MES $100 SL9":  (2,  36, 8,  1,  18, 3),
    "MES $100 SL10": (2,  40, 8,  1,  20, 3),
}

MGC_DATA = {
    # MGC $400
    "MGC $400 SL4":  (5,  40,  20, 5,  30, 10),
    "MGC $400 SL5":  (4,  50,  20, 4,  30, 8),
    "MGC $400 SL6":  (3,  60,  20, 3,  30, 6),
    "MGC $400 SL7":  (3,  70,  20, 2,  35, 5),
    "MGC $400 SL8":  (3,  80,  20, 2,  40, 5),
    "MGC $400 SL9":  (2,  90,  20, 2,  45, 4),
    "MGC $400 SL10": (2,  100, 20, 2,  50, 4),
    # MGC $200
    "MGC $200 SL4":  (3,  40,  20, 2,  30, 5),
    "MGC $200 SL5":  (2,  50,  20, 2,  30, 4),
    "MGC $200 SL6":  (2,  60,  20, 2,  30, 4),
    "MGC $200 SL7":  (2,  70,  20, 2,  35, 4),
    "MGC $200 SL8":  (2,  80,  20, 2,  40, 4),
    "MGC $200 SL9":  (2,  90,  20, 2,  45, 4),
    "MGC $200 SL10": (2,  100, 20, 2,  50, 4),
    # MGC $100
    "MGC $100 SL4":  (2,  40,  20, 2,  30, 4),
    "MGC $100 SL5":  (2,  50,  20, 2,  30, 4),
    "MGC $100 SL6":  (2,  60,  20, 2,  30, 4),
    "MGC $100 SL7":  (2,  70,  20, 2,  35, 4),
    "MGC $100 SL8":  (2,  80,  20, 2,  40, 4),
    "MGC $100 SL9":  (2,  90,  20, 2,  45, 4),
    "MGC $100 SL10": (2,  100, 20, 2,  50, 4),
}


def stop_strategy_xml(sop: dict, indent: str) -> str:
    steps_xml = "\n".join(
        f"{indent}    <AutoTrailStep>"
        f"<Frequency>{f}</Frequency>"
        f"<ProfitTrigger>{pt}</ProfitTrigger>"
        f"<StopLoss>{sl}</StopLoss>"
        f"</AutoTrailStep>"
        for f, pt, sl in sop["steps"]
    )
    return (
        f"{indent}<StopStrategy>\n"
        f"{indent}  <AutoBreakEvenPlus>{sop['be_plus']}</AutoBreakEvenPlus>\n"
        f"{indent}  <AutoBreakEvenProfitTrigger>{sop['be_trigger']}</AutoBreakEvenProfitTrigger>\n"
        f"{indent}  <AutoTrailSteps>\n"
        f"{steps_xml}\n"
        f"{indent}  </AutoTrailSteps>\n"
        f"{indent}  <IsSimStopEnabled>true</IsSimStopEnabled>\n"
        f"{indent}  <VolumeTrigger>1</VolumeTrigger>\n"
        f"{indent}  <Template>{sop['tpl']}</Template>\n"
        f"{indent}</StopStrategy>"
    )


def build_brackets_section(q1, sl_ticks, t1_ticks, sop_t1, q2, t2_ticks, sop_t2) -> str:
    ind = "          "  # 10 spaces (matches original file indentation)
    ss_t1 = stop_strategy_xml(sop_t1, ind)
    ss_t2 = stop_strategy_xml(sop_t2, ind)
    return (
        "      <Bracket>\n"
        f"        <Quantity>{q1}</Quantity>\n"
        f"        <StopLoss>{sl_ticks}</StopLoss>\n"
        f"{ss_t1}\n"
        f"        <Target>{t1_ticks}</Target>\n"
        "      </Bracket>\n"
        "      <Bracket>\n"
        f"        <Quantity>{q2}</Quantity>\n"
        f"        <StopLoss>{sl_ticks}</StopLoss>\n"
        f"{ss_t2}\n"
        f"        <Target>{t2_ticks}</Target>\n"
        "      </Bracket>"
    )


def fix_file(fname_key: str, data: tuple, sop: dict):
    q1, sl_ticks, t1_ticks, q2, t2_ticks, entry_qty = data
    filepath = os.path.join(NT_PATH, fname_key + ".xml")

    with open(filepath, "r", encoding="utf-8") as fh:
        content = fh.read()

    # Build the new <Brackets>...</Brackets> block
    new_brackets_inner = build_brackets_section(q1, sl_ticks, t1_ticks, sop, q2, t2_ticks, sop)
    new_brackets_block = f"    <Brackets>\n{new_brackets_inner}\n    </Brackets>"

    # Replace the entire <Brackets>...</Brackets> section (including any corruption)
    new_content = re.sub(
        r"    <Brackets>.*?</Brackets>",
        new_brackets_block,
        content,
        flags=re.DOTALL
    )

    with open(filepath, "w", encoding="utf-8", newline="") as fh:
        fh.write(new_content)
    print(f"FIXED {fname_key}.xml")


# Fix all MES files
for fname_key, data in MES_DATA.items():
    sl = int(re.search(r"SL(\d+)", fname_key).group(1))
    sop = MES_SOP[sl]
    fix_file(fname_key, data, sop)

# Fix all MGC files
for fname_key, data in MGC_DATA.items():
    sl = int(re.search(r"SL(\d+)", fname_key).group(1))
    sop = MGC_SOP[sl]
    fix_file(fname_key, data, sop)

print("\nAll done.")
