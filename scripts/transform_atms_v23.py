"""
Transform MES and MGC ATMs + Stop Strategies to 2-target design.

Rules:
- T1 ticks: SL4=1.5pt, +0.5pt per level (SL5=2.0, ..., SL10=4.5) converted to ticks
- T2 ticks: SL/2 in points converted to ticks  (= T1 + 0.5pt always)
- BE Trigger = T1 ticks
- MES BEPlus: 0 -> 1;  MGC BEPlus: stays 2
- Quantities: total = sum of old brackets; even -> T1=T2=total/2; odd -> T1=(total+1)//2, T2=(total-1)//2
- Remove old bracket 1 (quick scalp), redistribute its contracts
"""

import os, re
from pathlib import Path

NT_BASE = Path(os.path.expanduser("~")) / "Documents" / "NinjaTrader 8" / "templates"
ATM_DIR = NT_BASE / "AtmStrategy"
SS_DIR  = NT_BASE / "StopStrategy"

TICKS_PER_PT = {"MES": 4, "MGC": 10}

# T1 points per SL level: SL4=1.5, SL5=2.0, ..., SL10=4.5
def t1_pts(sl: int) -> float:
    return 1.0 + sl * 0.5   # SL4 -> 1.0+2.0=3.0? No: 1.5+0.5*(sl-4)
def t1_pts(sl: int) -> float:
    return 1.5 + 0.5 * (sl - 4)

def t2_pts(sl: int) -> float:
    return sl / 2.0   # half the SL size in points

def pts_to_ticks(pts: float, instrument: str) -> int:
    return round(pts * TICKS_PER_PT[instrument])

# Trail steps from existing ATMs - keep unchanged, just embed in new structure
# MES trail steps per SL (from current Scotty-SLx files)
MES_TRAIL = {
    4:  [("4","8","12"),  ("2","13","8"),  ("1","16","2")],
    5:  [("4","10","12"), ("2","17","8"),  ("1","20","2")],
    6:  [("4","12","12"), ("2","20","8"),  ("1","24","2")],
    7:  [("4","14","12"), ("2","23","8"),  ("1","28","2")],
    8:  [("4","16","12"), ("2","27","8"),  ("1","32","2")],
    9:  [("4","18","12"), ("2","30","8"),  ("1","36","2")],
    10: [("4","20","12"), ("2","33","8"),  ("1","40","2")],
}
# MGC trail steps (from current MGC Scotty SOP - same for all SLs)
MGC_TRAIL = {
    sl: [("2","20","5"), ("2","30","5"), ("1","40","2")]
    for sl in range(4, 11)
}

def trail_xml(steps, indent="            "):
    lines = []
    for freq, pt, sl in steps:
        lines.append(f"{indent}<AutoTrailStep><Frequency>{freq}</Frequency><ProfitTrigger>{pt}</ProfitTrigger><StopLoss>{sl}</StopLoss></AutoTrailStep>")
    return "\n".join(lines)

def stop_strategy_xml(be_trigger, be_plus, trail_steps, template_name, indent="          "):
    i = indent
    return f"""{i}<StopStrategy>
{i}  <AutoBreakEvenPlus>{be_plus}</AutoBreakEvenPlus>
{i}  <AutoBreakEvenProfitTrigger>{be_trigger}</AutoBreakEvenProfitTrigger>
{i}  <AutoTrailSteps>
{trail_xml(trail_steps, indent + "    ")}
{i}  </AutoTrailSteps>
{i}  <IsSimStopEnabled>true</IsSimStopEnabled>
{i}  <VolumeTrigger>1</VolumeTrigger>
{i}  <Template>{template_name}</Template>
{i}</StopStrategy>"""

def bracket_xml(qty, sl_ticks, target, stop_strat_xml=None, indent="      "):
    i = indent
    inner = f"""{i}<Bracket>
{i}  <Quantity>{qty}</Quantity>
{i}  <StopLoss>{sl_ticks}</StopLoss>"""
    if stop_strat_xml:
        inner += f"\n{stop_strat_xml}"
    inner += f"\n{i}  <Target>{target}</Target>\n{i}</Bracket>"
    return inner

def make_atm_xml(template_name, sl_ticks, t1_qty, t2_qty, t1_ticks, t2_ticks,
                  be_trigger, be_plus, trail_steps, ss_template_name, entry_qty,
                  description):
    ss = stop_strategy_xml(be_trigger, be_plus, trail_steps, ss_template_name)
    b1 = bracket_xml(t1_qty, sl_ticks, t1_ticks)
    b2 = bracket_xml(t2_qty, sl_ticks, t2_ticks, ss)
    return f"""<?xml version="1.0" encoding="utf-8"?>
<NinjaTrader>
  <AtmStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <IsVisible>true</IsVisible>
    <calculate2>OnBarClose</calculate2>
    <AreLinesConfigurable>true</AreLinesConfigurable>
    <ArePlotsConfigurable>true</ArePlotsConfigurable>
    <BarsToLoad>0</BarsToLoad>
    <Calculate>OnBarClose</Calculate>
    <Displacement>0</Displacement>
    <DisplayInDataBox>true</DisplayInDataBox>
    <From>2099-12-01T00:00:00</From>
    <IsAutoScale>true</IsAutoScale>
    <Lines />
    <MaximumBarsLookBack>TwoHundredFiftySix</MaximumBarsLookBack>
    <Name>AtmStrategy</Name>
    <Panel>0</Panel>
    <Plots />
    <ScaleJustification>Right</ScaleJustification>
    <ShowTransparentPlotsInDataBox>false</ShowTransparentPlotsInDataBox>
    <To>1800-01-01T00:00:00</To>
    <IsDataSeriesRequired>false</IsDataSeriesRequired>
    <IsOverlay>false</IsOverlay>
    <SelectedValueSeries>0</SelectedValueSeries>
    <Gtd>1800-01-01T00:00:00</Gtd>
    <Template>{template_name}</Template>
    <TimeInForce>Gtc</TimeInForce>
    <BarsRequiredToTrade>0</BarsRequiredToTrade>
    <Category>Atm</Category>
    <ConnectionLossHandling>KeepRunning</ConnectionLossHandling>
    <DaysToLoad>1</DaysToLoad>
    <DefaultQuantity>1</DefaultQuantity>
    <DisconnectDelaySeconds>0</DisconnectDelaySeconds>
    <EntriesPerDirection>1</EntriesPerDirection>
    <EntryHandling>AllEntries</EntryHandling>
    <ExitOnSessionCloseSeconds>0</ExitOnSessionCloseSeconds>
    <IncludeCommission>false</IncludeCommission>
    <IsAggregated>false</IsAggregated>
    <IsExitOnSessionCloseStrategy>false</IsExitOnSessionCloseStrategy>
    <IsFillLimitOnTouch>false</IsFillLimitOnTouch>
    <IsOptimizeDataSeries>false</IsOptimizeDataSeries>
    <IsStableSession>false</IsStableSession>
    <IsTickReplay>false</IsTickReplay>
    <IsTradingHoursBreakLineVisible>false</IsTradingHoursBreakLineVisible>
    <IsWaitUntilFlat>false</IsWaitUntilFlat>
    <NumberRestartAttempts>0</NumberRestartAttempts>
    <OptimizationPeriod>10</OptimizationPeriod>
    <OrderFillResolution>High</OrderFillResolution>
    <OrderFillResolutionType>Tick</OrderFillResolutionType>
    <OrderFillResolutionValue>1</OrderFillResolutionValue>
    <RestartsWithinMinutes>0</RestartsWithinMinutes>
    <SetOrderQuantity>Strategy</SetOrderQuantity>
    <Slippage>0</Slippage>
    <StartBehavior>AdoptAccountPosition</StartBehavior>
    <StopTargetHandling>PerEntryExecution</StopTargetHandling>
    <SupportsOptimizationGraph>false</SupportsOptimizationGraph>
    <TestPeriod>28</TestPeriod>
    <TradingHoursSerializable />
    <Brackets>
{b1}
{b2}
    </Brackets>
    <EntryQuantity>{entry_qty}</EntryQuantity>
    <CalculationMode>Ticks</CalculationMode>
    <ChaseLimit>1</ChaseLimit>
    <InitialTickSize>0</InitialTickSize>
    <IsChase>false</IsChase>
    <IsChaseIfTouched>true</IsChaseIfTouched>
    <IsTargetChase>false</IsTargetChase>
    <ReverseAtStop>false</ReverseAtStop>
    <ReverseAtTarget>false</ReverseAtTarget>
    <UseMitForProfit>false</UseMitForProfit>
    <UseStopLimitForStopLossOrders>false</UseStopLimitForStopLossOrders>
    <AtmSelector>508659aee0554887b3aaf1944107b792</AtmSelector>
    <OnBehalfOf />
    <ReverseAtStopStrategyId>-1</ReverseAtStopStrategyId>
    <ReverseAtTargetStrategyId>-1</ReverseAtTargetStrategyId>
    <ShadowStrategyStrategyId>-1</ShadowStrategyStrategyId>
    <ShadowTemplate />
  </AtmStrategy>
</NinjaTrader>"""

def make_ss_xml(template_name, be_trigger, be_plus, trail_steps):
    return f"""<?xml version="1.0" encoding="utf-8"?>
<NinjaTrader>
  <StopStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <AutoBreakEvenPlus>{be_plus}</AutoBreakEvenPlus>
    <AutoBreakEvenProfitTrigger>{be_trigger}</AutoBreakEvenProfitTrigger>
    <AutoTrailSteps>
{trail_xml(trail_steps, "      ")}
    </AutoTrailSteps>
    <IsSimStopEnabled>true</IsSimStopEnabled>
    <VolumeTrigger>1</VolumeTrigger>
    <Template>{template_name}</Template>
  </StopStrategy>
</NinjaTrader>"""

# Current total bracket quantities per ATM (bracket sums from actual XML)
CURRENT_TOTALS = {
    # MES
    ("MES", 100, 4):  5,
    ("MES", 100, 5):  4,
    ("MES", 100, 6):  4,
    ("MES", 100, 7):  3,
    ("MES", 100, 8):  3,
    ("MES", 100, 9):  3,
    ("MES", 100, 10): 3,
    ("MES", 200, 4):  10,
    ("MES", 200, 5):  8,
    ("MES", 200, 6):  7,
    ("MES", 200, 7):  6,
    ("MES", 200, 8):  5,
    ("MES", 200, 9):  5,
    ("MES", 200, 10): 4,
    ("MES", 400, 4):  20,
    ("MES", 400, 5):  16,
    ("MES", 400, 6):  13,
    ("MES", 400, 7):  11,
    ("MES", 400, 8):  10,
    ("MES", 400, 9):  8,
    ("MES", 400, 10): 8,
    # MGC (using bracket sums as authoritative)
    ("MGC", 100, 4):  4,
    ("MGC", 100, 5):  4,
    ("MGC", 100, 6):  4,
    ("MGC", 100, 7):  4,
    ("MGC", 100, 8):  4,
    ("MGC", 100, 9):  4,
    ("MGC", 100, 10): 4,
    ("MGC", 200, 4):  5,
    ("MGC", 200, 5):  4,
    ("MGC", 200, 6):  4,
    ("MGC", 200, 7):  4,
    ("MGC", 200, 8):  4,
    ("MGC", 200, 9):  4,
    ("MGC", 200, 10): 4,
    ("MGC", 400, 4):  10,
    ("MGC", 400, 5):  8,
    ("MGC", 400, 6):  6,
    ("MGC", 400, 7):  5,
    ("MGC", 400, 8):  5,
    ("MGC", 400, 9):  4,
    ("MGC", 400, 10): 4,
}

def qty_split(total):
    if total % 2 == 0:
        return total // 2, total // 2
    else:
        return (total + 1) // 2, (total - 1) // 2

written = []
errors = []

for instrument in ["MES", "MGC"]:
    tpp = TICKS_PER_PT[instrument]
    be_plus = 1 if instrument == "MES" else 2
    trail_map = MES_TRAIL if instrument == "MES" else MGC_TRAIL

    for acct in [100, 200, 400]:
        for sl in range(4, 11):
            key = (instrument, acct, sl)
            if key not in CURRENT_TOTALS:
                continue

            sl_pts = sl  # SL level = stop loss size in points
            sl_ticks = sl_pts * tpp

            t1p = t1_pts(sl)
            t2p = t2_pts(sl)
            t1t = pts_to_ticks(t1p, instrument)
            t2t = pts_to_ticks(t2p, instrument)
            be_trigger = t1t

            total = CURRENT_TOTALS[key]
            t1_qty, t2_qty = qty_split(total)

            template_name = f"{instrument} ${acct} SL{sl}"
            if instrument == "MES":
                ss_template = f"Scotty-SL{sl}"
            else:
                ss_template = f"MGC Scotty SL{sl}"

            trail = trail_map[sl]

            desc_t1 = f"T1={t1t}T={t1p}pts x{t1_qty}c"
            desc_t2 = f"T2={t2t}T={t2p}pts x{t2_qty}c"
            description = f"{template_name} | {desc_t1} | {desc_t2} | BE@+{be_trigger}T->{be_plus}T flat | Apex 50k PA"

            xml = make_atm_xml(
                template_name=template_name,
                sl_ticks=sl_ticks,
                t1_qty=t1_qty,
                t2_qty=t2_qty,
                t1_ticks=t1t,
                t2_ticks=t2t,
                be_trigger=be_trigger,
                be_plus=be_plus,
                trail_steps=trail,
                ss_template_name=ss_template,
                entry_qty=total,
                description=description,
            )

            fname = ATM_DIR / f"{template_name}.xml"
            try:
                fname.write_text(xml, encoding="utf-8")
                written.append(str(fname.name))
            except Exception as e:
                errors.append(f"{fname.name}: {e}")

print(f"Written {len(written)} ATM files")
if errors:
    print("ERRORS:", errors)
else:
    print("No errors")
for w in written:
    print(" ", w)
