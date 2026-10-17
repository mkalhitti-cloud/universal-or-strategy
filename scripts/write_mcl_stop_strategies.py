"""
Write MCL StopStrategy template XML files and update all MCL ATM XMLs.

Rules (matching MES/MGC baseline):
- AutoBreakEvenProfitTrigger = T1 ticks exactly
- AutoBreakEvenPlus = 3 (3-tick buffer above entry)
- IsSimStopEnabled = true, VolumeTrigger = 1
- BOTH brackets get the same stop strategy
- Trail: 3 steps scaled per SL level
- SL2 and SL3 skipped (single-bracket ATMs)
"""
import os, re

ATM_DIR = os.path.expandvars(r"%USERPROFILE%\Documents\NinjaTrader 8\templates\AtmStrategy")
SS_DIR  = os.path.expandvars(r"%USERPROFILE%\Documents\NinjaTrader 8\templates\StopStrategy")

# MCL: $1.00/tick, 100 ticks/point
# Columns: t1, t2, step1_pt, step1_sl, step2_pt, step2_sl, step3_sl
# Step3 PT = T2 (trail to T2 level); step SLs scale with instrument size
MCL_SOP = {
    1: dict(t1=32, t2=50, s1_pt=38, s1_sl=20, s2_pt=45, s2_sl=15, s3_pt=50, s3_sl=10),
    4: dict(t1=15, t2=20, s1_pt=18, s1_sl=8,  s2_pt=22, s2_sl=5,  s3_pt=20, s3_sl=3),
    5: dict(t1=18, t2=25, s1_pt=21, s1_sl=8,  s2_pt=26, s2_sl=5,  s3_pt=25, s3_sl=3),
    6: dict(t1=25, t2=30, s1_pt=30, s1_sl=8,  s2_pt=38, s2_sl=5,  s3_pt=30, s3_sl=3),
    7: dict(t1=31, t2=35, s1_pt=37, s1_sl=8,  s2_pt=44, s2_sl=5,  s3_pt=35, s3_sl=3),
    8: dict(t1=27, t2=40, s1_pt=32, s1_sl=8,  s2_pt=40, s2_sl=5,  s3_pt=40, s3_sl=3),
}

# ATM data: fname_key -> (q1, sl_ticks, t1_ticks, q2, t2_ticks, entry_qty)
MCL_DATA = {
    "MCL $400 SL1": (2, 100, 32, 2, 50, 4),
    "MCL $400 SL4": (5, 40,  15, 5, 20, 10),
    "MCL $400 SL5": (4, 50,  18, 4, 25, 8),
    "MCL $400 SL6": (3, 60,  25, 3, 30, 6),
    "MCL $400 SL7": (3, 70,  31, 2, 35, 5),
    "MCL $400 SL8": (3, 80,  27, 2, 40, 5),
    "MCL $200 SL1": (1, 100, 32, 1, 50, 2),
    "MCL $200 SL4": (3, 40,  15, 2, 20, 5),
    "MCL $200 SL5": (2, 50,  18, 2, 25, 4),
    "MCL $200 SL6": (2, 60,  25, 1, 30, 3),
    "MCL $200 SL7": (2, 70,  31, 1, 35, 3),
    "MCL $200 SL8": (1, 80,  27, 1, 40, 2),
}


def ss_template_xml(sl: int) -> str:
    s = MCL_SOP[sl]
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<NinjaTrader>\n'
        '  <StopStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" '
        'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">\n'
        f'    <AutoBreakEvenPlus>3</AutoBreakEvenPlus>\n'
        f'    <AutoBreakEvenProfitTrigger>{s["t1"]}</AutoBreakEvenProfitTrigger>\n'
        '    <AutoTrailSteps>\n'
        f'      <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{s["s1_pt"]}</ProfitTrigger><StopLoss>{s["s1_sl"]}</StopLoss></AutoTrailStep>\n'
        f'      <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{s["s2_pt"]}</ProfitTrigger><StopLoss>{s["s2_sl"]}</StopLoss></AutoTrailStep>\n'
        f'      <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>{s["s3_pt"]}</ProfitTrigger><StopLoss>{s["s3_sl"]}</StopLoss></AutoTrailStep>\n'
        '    </AutoTrailSteps>\n'
        '    <IsSimStopEnabled>true</IsSimStopEnabled>\n'
        '    <VolumeTrigger>1</VolumeTrigger>\n'
        f'    <Template>MCL SL{sl}</Template>\n'
        '  </StopStrategy>\n'
        '</NinjaTrader>'
    )


def ss_inline_xml(sl: int, indent: str) -> str:
    """Inline StopStrategy block for embedding inside a Bracket."""
    s = MCL_SOP[sl]
    i = indent
    return (
        f"{i}<StopStrategy>\n"
        f"{i}  <AutoBreakEvenPlus>3</AutoBreakEvenPlus>\n"
        f"{i}  <AutoBreakEvenProfitTrigger>{s['t1']}</AutoBreakEvenProfitTrigger>\n"
        f"{i}  <AutoTrailSteps>\n"
        f"{i}    <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{s['s1_pt']}</ProfitTrigger><StopLoss>{s['s1_sl']}</StopLoss></AutoTrailStep>\n"
        f"{i}    <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{s['s2_pt']}</ProfitTrigger><StopLoss>{s['s2_sl']}</StopLoss></AutoTrailStep>\n"
        f"{i}    <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>{s['s3_pt']}</ProfitTrigger><StopLoss>{s['s3_sl']}</StopLoss></AutoTrailStep>\n"
        f"{i}  </AutoTrailSteps>\n"
        f"{i}  <IsSimStopEnabled>true</IsSimStopEnabled>\n"
        f"{i}  <VolumeTrigger>1</VolumeTrigger>\n"
        f"{i}  <Template>MCL SL{sl}</Template>\n"
        f"{i}</StopStrategy>\n"
    )


def build_brackets(q1, sl_ticks, t1_ticks, q2, t2_ticks, sl: int) -> str:
    ss = ss_inline_xml(sl, "        ")
    return (
        "    <Brackets>\n"
        "      <Bracket>\n"
        f"        <Quantity>{q1}</Quantity>\n"
        f"        <StopLoss>{sl_ticks}</StopLoss>\n"
        f"{ss}"
        f"        <Target>{t1_ticks}</Target>\n"
        "      </Bracket>\n"
        "      <Bracket>\n"
        f"        <Quantity>{q2}</Quantity>\n"
        f"        <StopLoss>{sl_ticks}</StopLoss>\n"
        f"{ss}"
        f"        <Target>{t2_ticks}</Target>\n"
        "      </Bracket>\n"
        "    </Brackets>"
    )


# ── 1. Write StopStrategy template files ──────────────────────────────────────
for sl in [1, 4, 5, 6, 7, 8]:
    path = os.path.join(SS_DIR, f"MCL SL{sl}.xml")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(ss_template_xml(sl))
    print(f"SS WRITTEN: MCL SL{sl}.xml  BETrigger={MCL_SOP[sl]['t1']}  BEPlus=3")

# ── 2. Update ATM XML files ────────────────────────────────────────────────────
for fname_key, (q1, sl_ticks, t1_ticks, q2, t2_ticks, entry_qty) in MCL_DATA.items():
    sl = int(re.search(r"SL(\d+)", fname_key).group(1))
    filepath = os.path.join(ATM_DIR, fname_key + ".xml")

    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    new_brackets = build_brackets(q1, sl_ticks, t1_ticks, q2, t2_ticks, sl)

    new_content = re.sub(
        r"    <Brackets>.*?</Brackets>",
        new_brackets,
        content,
        flags=re.DOTALL
    )

    with open(filepath, "w", encoding="utf-8", newline="") as f:
        f.write(new_content)
    print(f"ATM UPDATED: {fname_key}.xml  BETrigger={t1_ticks}  BEPlus=3")

print("\nAll done.")
