"""
write_mnq_atm_xmls.py
Generates MNQ ATM XML files ($100/$200/$300/$400 series, SL20-SL100) and
MNQ DTDB ATMs for all 4 series, plus 8 MNQ Scotty stop strategy XMLs.

MNQ Contract: tick=$0.50, point=$2.00, 4 ticks/pt

Naming: SL number = POINT count (SL20=20pts=80T, SL40=40pts=160T, etc.)
Targets: T1=25% SL (in ticks), T2=50% SL (in ticks)
DTDB: 10-point stop (40T), T1=2.5pt(10T), T2=5pt(20T), SL20-baseline quantities
"""
import os, sys
sys.stdout.reconfigure(encoding='utf-8')

ATM_DIR = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'
SS_DIR  = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\StopStrategy'

# ── Contract constants ────────────────────────────────────────────────────────
TICK_VAL   = 0.50   # $ per tick
TICKS_PER_PT = 4   # MNQ: 4 ticks per point

# ── SL definitions ───────────────────────────────────────────────────────────
# (sl_label, sl_pts, sl_ticks, t1_tgt_ticks, t2_tgt_ticks, scotty_template)
# T1 = 25% SL in ticks, T2 = 50% SL in ticks
SL_DEFS = [
    ('SL20',   20,   80,  20,  40, 'MNQ Scotty-SL20'),
    ('SL40',   40,  160,  40,  80, 'MNQ Scotty-SL40'),
    ('SL50',   50,  200,  50, 100, 'MNQ Scotty-SL50'),
    ('SL60',   60,  240,  60, 120, 'MNQ Scotty-SL60'),
    ('SL70',   70,  280,  70, 140, 'MNQ Scotty-SL70'),
    ('SL80',   80,  320,  80, 160, 'MNQ Scotty-SL80'),
    ('SL90',   90,  360,  90, 180, 'MNQ Scotty-SL90'),
    ('SL100', 100,  400, 100, 200, 'MNQ Scotty-SL100'),
]

# ── Quantities per series ─────────────────────────────────────────────────────
# Keyed by (series_dollars, sl_label) -> (total, t1q, t2q)
# t2q=0 = single-target ATM (T2 bracket qty 0)
QUANTITIES = {
    100: {
        'SL20':  (2,  1, 1),   # $80 risk
        'SL40':  (2,  1, 1),   # $160 risk (+1 bumped per design)
        'SL50':  (1,  1, 0),   # $100 risk, single target
        'SL60':  (2,  1, 1),   # $240 risk (+1 bumped per design)
        'SL70':  (1,  1, 0),   # $140 risk (+1 bumped per design), single target
        'SL80':  (1,  1, 0),   # $160 risk (+1 bumped per design), single target
        'SL90':  (1,  1, 0),   # $180 risk, single target
        'SL100': (1,  1, 0),   # $200 risk, single target
    },
    200: {
        'SL20':  (5,  3, 2),   # $200 risk
        'SL40':  (2,  1, 1),   # $160 risk
        'SL50':  (2,  1, 1),   # $200 risk
        'SL60':  (1,  1, 0),   # $120 risk, single target
        'SL70':  (1,  1, 0),   # $140 risk, single target
        'SL80':  (1,  1, 0),   # $160 risk, single target
        'SL90':  (1,  1, 0),   # $180 risk, single target
        'SL100': (1,  1, 0),   # $200 risk, single target
    },
    300: {
        'SL20':  (7,  4, 3),   # $280 risk
        'SL40':  (3,  2, 1),   # $240 risk
        'SL50':  (3,  2, 1),   # $300 risk
        'SL60':  (2,  1, 1),   # $240 risk
        'SL70':  (2,  1, 1),   # $280 risk
        'SL80':  (1,  1, 0),   # $160 risk, single target
        'SL90':  (1,  1, 0),   # $180 risk, single target
        'SL100': (1,  1, 0),   # $200 risk, single target
    },
    400: {
        'SL20':  (10, 5, 5),   # $400 risk
        'SL40':  (5,  3, 2),   # $400 risk
        'SL50':  (4,  2, 2),   # $400 risk
        'SL60':  (3,  2, 1),   # $360 risk
        'SL70':  (2,  1, 1),   # $280 risk
        'SL80':  (2,  1, 1),   # $320 risk
        'SL90':  (2,  1, 1),   # $360 risk
        'SL100': (2,  1, 1),   # $400 risk
    },
}

# ── Scotty stop strategy params ───────────────────────────────────────────────
# All tick values scaled 4× vs original (SL now in points, 4T per pt)
# SL20-SL60: 2-step trail
# SL70-SL100: 3-step proportional trail
# Format: (be_trigger, be_plus, [(pt, sl, freq), ...])
SCOTTY = {
    'MNQ Scotty-SL20':  ( 80, 20, [(100,  40, 5), (120,  20, 5)]),
    'MNQ Scotty-SL40':  ( 80, 20, [(100,  40, 5), (120,  20, 5)]),
    'MNQ Scotty-SL50':  ( 80, 20, [(100,  40, 5), (120,  20, 5)]),
    'MNQ Scotty-SL60':  ( 80, 20, [(100,  40, 5), (120,  20, 5)]),
    'MNQ Scotty-SL70':  (100, 20, [(140,  60, 4), (200,  32, 3), (280, 20, 2)]),
    'MNQ Scotty-SL80':  (120, 20, [(160,  80, 4), (240,  40, 3), (320, 20, 2)]),
    'MNQ Scotty-SL90':  (140, 20, [(180,  88, 4), (268,  48, 3), (360, 20, 2)]),
    'MNQ Scotty-SL100': (160, 20, [(200, 100, 4), (300,  52, 3), (400, 20, 2)]),
}

# ── DTDB data ─────────────────────────────────────────────────────────────────
# 10-point stop (40T), T1=2.5pt(10T), T2=5pt(20T), SL20-baseline quantities
# (series, sl_ticks, total, t1q, t2q, t1_tgt, t2_tgt)
MNQ_DTDB = [
    (100,  40,  5,  3, 2, 10, 20),
    (200,  40, 10,  5, 5, 10, 20),
    (300,  40, 14,  7, 7, 10, 20),
    (400,  40, 20, 10, 10, 10, 20),
]
DTDB_SS = 'MNQ Scotty-SL20'

# ── XML templates ─────────────────────────────────────────────────────────────
XML_HEADER = '''<?xml version="1.0" encoding="utf-8"?>
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
    <Gtd>1800-01-01T00:00:00</Gtd>'''

XML_FOOTER = '''    <CalculationMode>Ticks</CalculationMode>
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
</NinjaTrader>'''


def stop_strategy_block(tmpl_name):
    be_trig, be_plus, steps = SCOTTY[tmpl_name]
    step_xml = ''
    for pt, sl, freq in steps:
        step_xml += f'              <AutoTrailStep><Frequency>{freq}</Frequency><ProfitTrigger>{pt}</ProfitTrigger><StopLoss>{sl}</StopLoss></AutoTrailStep>\n'
    return f'''          <StopStrategy>
            <AutoBreakEvenPlus>{be_plus}</AutoBreakEvenPlus>
            <AutoBreakEvenProfitTrigger>{be_trig}</AutoBreakEvenProfitTrigger>
            <AutoTrailSteps>
{step_xml.rstrip()}
            </AutoTrailSteps>
            <IsSimStopEnabled>true</IsSimStopEnabled>
            <VolumeTrigger>1</VolumeTrigger>
            <Template>{tmpl_name}</Template>
          </StopStrategy>'''


def build_bracket_xml(qty, sl_ticks, tgt_ticks, ss_block):
    """Build a single <Bracket> element."""
    return f'''      <Bracket>
        <Quantity>{qty}</Quantity>
        <StopLoss>{sl_ticks}</StopLoss>
{ss_block}
        <Target>{tgt_ticks}</Target>
      </Bracket>'''


def build_atm_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, scotty_tmpl):
    ss = stop_strategy_block(scotty_tmpl)
    t1_bracket = build_bracket_xml(t1q, sl_ticks, t1_tgt, ss)
    brackets_xml = t1_bracket
    if t2q > 0:
        t2_bracket = build_bracket_xml(t2q, sl_ticks, t2_tgt, ss)
        brackets_xml += f'\n{t2_bracket}'
    return f'''{XML_HEADER}
    <Template>{name}</Template>
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
{brackets_xml}
    </Brackets>
    <EntryQuantity>{total}</EntryQuantity>
{XML_FOOTER}'''


def build_ss_xml(tmpl_name):
    be_trig, be_plus, steps = SCOTTY[tmpl_name]
    step_xml = ''
    for pt, sl, freq in steps:
        step_xml += f'      <AutoTrailStep><Frequency>{freq}</Frequency><ProfitTrigger>{pt}</ProfitTrigger><StopLoss>{sl}</StopLoss></AutoTrailStep>\n'
    return f'''<?xml version="1.0" encoding="utf-8"?>
<NinjaTrader>
  <StopStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <AutoBreakEvenPlus>{be_plus}</AutoBreakEvenPlus>
    <AutoBreakEvenProfitTrigger>{be_trig}</AutoBreakEvenProfitTrigger>
    <AutoTrailSteps>
{step_xml.rstrip()}
    </AutoTrailSteps>
    <IsSimStopEnabled>true</IsSimStopEnabled>
    <VolumeTrigger>1</VolumeTrigger>
    <Template>{tmpl_name}</Template>
  </StopStrategy>
</NinjaTrader>'''


written_atm = 0
written_ss  = 0

# ── Write Scotty stop strategy XMLs ──────────────────────────────────────────
for tmpl_name in SCOTTY:
    xml = build_ss_xml(tmpl_name)
    out = os.path.join(SS_DIR, f'{tmpl_name}.xml')
    with open(out, 'w', encoding='utf-8') as fh:
        fh.write(xml)
    be_trig, be_plus, steps = SCOTTY[tmpl_name]
    print(f'SS  {tmpl_name}.xml  BETrig={be_trig}T  BEPlus=+{be_plus}T  steps={len(steps)}')
    written_ss += 1

print()

# ── Write standard ATM XMLs ($100-$400, SL20-SL100) ──────────────────────────
for series in [100, 200, 300, 400]:
    for sl_label, sl_pts, sl_ticks, t1_tgt, t2_tgt, scotty_tmpl in SL_DEFS:
        total, t1q, t2q = QUANTITIES[series][sl_label]
        name = f'MNQ ${series} {sl_label}'
        xml  = build_atm_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, scotty_tmpl)
        out  = os.path.join(ATM_DIR, f'{name}.xml')
        with open(out, 'w', encoding='utf-8') as fh:
            fh.write(xml)
        risk = total * sl_ticks * TICK_VAL
        t2_info = f'T2={t2q}x{t2_tgt}T' if t2q > 0 else 'T2=none'
        print(f'ATM {name}.xml  total={total}  T1={t1q}x{t1_tgt}T  {t2_info}  SL={sl_ticks}T({sl_pts}pt)  risk=${risk:.0f}')
        written_atm += 1

print()

# ── Write DTDB ATM XMLs ───────────────────────────────────────────────────────
for series, sl_ticks, total, t1q, t2q, t1_tgt, t2_tgt in MNQ_DTDB:
    name = f'MNQ ${series} DTDB'
    xml  = build_atm_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, DTDB_SS)
    out  = os.path.join(ATM_DIR, f'{name}.xml')
    with open(out, 'w', encoding='utf-8') as fh:
        fh.write(xml)
    risk = total * sl_ticks * TICK_VAL
    print(f'DTDB {name}.xml  total={total}  T1={t1q}x{t1_tgt}T  T2={t2q}x{t2_tgt}T  SL={sl_ticks}T  risk=${risk:.0f}')
    written_atm += 1

print(f'\n  Stop Strategies: {written_ss}   ATMs: {written_atm}   Total files: {written_ss + written_atm}')
