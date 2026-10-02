import os
from pathlib import Path

SS_DIR = Path(os.path.expanduser("~")) / "Documents" / "NinjaTrader 8" / "templates" / "StopStrategy"

MES_TRAIL = {
    4:  [("4","8","12"),  ("2","13","8"),  ("1","16","2")],
    5:  [("4","10","12"), ("2","17","8"),  ("1","20","2")],
    6:  [("4","12","12"), ("2","20","8"),  ("1","24","2")],
    7:  [("4","14","12"), ("2","23","8"),  ("1","28","2")],
    8:  [("4","16","12"), ("2","27","8"),  ("1","32","2")],
    9:  [("4","18","12"), ("2","30","8"),  ("1","36","2")],
    10: [("4","20","12"), ("2","33","8"),  ("1","40","2")],
}
MGC_TRAIL = {sl: [("2","20","5"), ("2","30","5"), ("1","40","2")] for sl in range(4,11)}

def t1_pts(sl):
    return 1.5 + 0.5 * (sl - 4)

def trail_xml(steps):
    lines = []
    for f, p, s in steps:
        lines.append(f"      <AutoTrailStep><Frequency>{f}</Frequency><ProfitTrigger>{p}</ProfitTrigger><StopLoss>{s}</StopLoss></AutoTrailStep>")
    return "\n".join(lines)

def make_ss_xml(template_name, be_trigger, be_plus, trail_steps):
    t = trail_xml(trail_steps)
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<NinjaTrader>\n'
        '  <StopStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">\n'
        f'    <AutoBreakEvenPlus>{be_plus}</AutoBreakEvenPlus>\n'
        f'    <AutoBreakEvenProfitTrigger>{be_trigger}</AutoBreakEvenProfitTrigger>\n'
        '    <AutoTrailSteps>\n'
        f'{t}\n'
        '    </AutoTrailSteps>\n'
        '    <IsSimStopEnabled>true</IsSimStopEnabled>\n'
        '    <VolumeTrigger>1</VolumeTrigger>\n'
        f'    <Template>{template_name}</Template>\n'
        '  </StopStrategy>\n'
        '</NinjaTrader>'
    )

written = []
for sl in range(4, 11):
    # MES Scotty-SLx: BEPlus=1, BETrigger=T1 in MES ticks (4 ticks/pt)
    t1t_mes = round(t1_pts(sl) * 4)
    name_mes = f"Scotty-SL{sl}"
    xml_mes = make_ss_xml(name_mes, t1t_mes, 1, MES_TRAIL[sl])
    (SS_DIR / f"{name_mes}.xml").write_text(xml_mes, encoding="utf-8")
    written.append(f"{name_mes}  BETrigger={t1t_mes} BEPlus=1")

    # MGC Scotty SLx: BEPlus=2, BETrigger=T1 in MGC ticks (10 ticks/pt)
    t1t_mgc = round(t1_pts(sl) * 10)
    name_mgc = f"MGC Scotty SL{sl}"
    xml_mgc = make_ss_xml(name_mgc, t1t_mgc, 2, MGC_TRAIL[sl])
    (SS_DIR / f"{name_mgc}.xml").write_text(xml_mgc, encoding="utf-8")
    written.append(f"{name_mgc}  BETrigger={t1t_mgc} BEPlus=2")

print(f"Written {len(written)} stop strategy files:")
for w in written:
    print(" ", w)
