"""
write_mnq_atm_xmls.py
Generates MNQ ATM XML files ($100/$200/$300/$400 series, SL20-SL100) and
MNQ DTDB ATMs for all 4 series, plus 8 MNQ Scotty stop strategy XMLs.

MNQ Contract: tick=$0.50, point=$2.00, 4 ticks/pt
Ratio vs MES: 2.5x more ticks for same dollar risk

Naming: SL number = tick count (SL20=20T, SL40=40T, etc.)
Targets: T1=20T, T2=30T for SL20-SL60; +5T per step from SL70 up
Quantities: same as MES equivalent series (accepting lower dollar risk on SL20)
DTDB: SL20-baseline quantities, tight 10T stop
"""
import os, sys
sys.stdout.reconfigure(encoding='utf-8')

ATM_DIR = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'
SS_DIR  = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\StopStrategy'

# ── Contract constants ────────────────────────────────────────────────────────
TICK_VAL = 0.50   # $ per tick

# ── SL definitions ───────────────────────────────────────────────────────────
# (sl_label, sl_ticks, t1_tgt, t2_tgt, scotty_template)
SL_DEFS = [
    ('SL20',  20,  20, 30, 'MNQ Scotty-SL20'),
    ('SL40',  40,  20, 30, 'MNQ Scotty-SL40'),
    ('SL50',  50,  20, 30, 'MNQ Scotty-SL50'),
    ('SL60',  60,  20, 30, 'MNQ Scotty-SL60'),
    ('SL70',  70,  25, 35, 'MNQ Scotty-SL70'),
    ('SL80',  80,  30, 40, 'MNQ Scotty-SL80'),
    ('SL90',  90,  35, 45, 'MNQ Scotty-SL90'),
    ('SL100', 100, 40, 50, 'MNQ Scotty-SL100'),
]

# ── Quantities per series (same as MES equivalent) ───────────────────────────
# Keyed by (series_dollars, sl_label) -> (total, t1q, t2q)
# SL20 uses same qty as SL40 (accepting lower dollar risk)
QUANTITIES = {
    100: {
        'SL20':  (5,  3, 2),
        'SL40':  (5,  3, 2),
        'SL50':  (4,  2, 2),
        'SL60':  (3,  2, 1),
        'SL70':  (3,  2, 1),
        'SL80':  (2,  1, 1),
        'SL90':  (2,  1, 1),
        'SL100': (2,  1, 1),
    },
    200: {
        'SL20':  (10, 5, 5),
        'SL40':  (10, 5, 5),
        'SL50':  (8,  4, 4),
        'SL60':  (6,  3, 3),
        'SL70':  (6,  3, 3),
        'SL80':  (4,  2, 2),
        'SL90':  (4,  2, 2),
        'SL100': (4,  2, 2),
    },
    300: {
        'SL20':  (14, 7, 7),
        'SL40':  (14, 7, 7),
        'SL50':  (12, 6, 6),
        'SL60':  (10, 5, 5),
        'SL70':  (8,  4, 4),
        'SL80':  (7,  4, 3),
        'SL90':  (6,  3, 3),
        'SL100': (6,  3, 3),
    },
    400: {
        'SL20':  (20, 10, 10),
        'SL40':  (20, 10, 10),
        'SL50':  (16, 8,  8),
        'SL60':  (12, 6,  6),
        'SL70':  (11, 6,  5),
        'SL80':  (9,  5,  4),
        'SL90':  (8,  4,  4),
        'SL100': (8,  4,  4),
    },
}

# ── Scotty stop strategy params ───────────────────────────────────────────────
# Small SLs (SL20-SL60): 2-step, fixed params matching Scotty's NQ template
# Large SLs (SL70-SL100): 3-step, proportionally scaled
# Format: (be_trigger, be_plus, [(pt, sl, freq), ...])
SCOTTY = {
    'MNQ Scotty-SL20':  (20, 5, [(25, 10, 5), (30,  5, 5)]),
    'MNQ Scotty-SL40':  (20, 5, [(25, 10, 5), (30,  5, 5)]),
    'MNQ Scotty-SL50':  (20, 5, [(25, 10, 5), (30,  5, 5)]),
    'MNQ Scotty-SL60':  (20, 5, [(25, 10, 5), (30,  5, 5)]),
    'MNQ Scotty-SL70':  (25, 5, [(35, 15, 4), (50,  8, 3), (70,  5, 2)]),
    'MNQ Scotty-SL80':  (30, 5, [(40, 20, 4), (60, 10, 3), (80,  5, 2)]),
    'MNQ Scotty-SL90':  (35, 5, [(45, 22, 4), (67, 12, 3), (90,  5, 2)]),
    'MNQ Scotty-SL100': (40, 5, [(50, 25, 4), (75, 13, 3), (100, 5, 2)]),
}

# ── DTDB data ─────────────────────────────────────────────────────────────────
# Tight 10T stop, SL20-baseline quantities, MNQ Scotty-SL20 stop strategy
MNQ_DTDB = [
    (100,  10, 5,  3, 2,  20, 30),
    (200,  10, 10, 5, 5,  20, 30),
    (300,  10, 14, 7, 7,  20, 30),
    (400,  10, 20, 10, 10, 20, 30),
]

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


def build_atm_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, scotty_tmpl):
    ss = stop_strategy_block(scotty_tmpl)
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
      <Bracket>
        <Quantity>{t1q}</Quantity>
        <StopLoss>{sl_ticks}</StopLoss>
{ss}
        <Target>{t1_tgt}</Target>
      </Bracket>
      <Bracket>
        <Quantity>{t2q}</Quantity>
        <StopLoss>{sl_ticks}</StopLoss>
{ss}
        <Target>{t2_tgt}</Target>
      </Bracket>
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
    for sl_label, sl_ticks, t1_tgt, t2_tgt, scotty_tmpl in SL_DEFS:
        total, t1q, t2q = QUANTITIES[series][sl_label]
        name = f'MNQ ${series} {sl_label}'
        xml  = build_atm_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, scotty_tmpl)
        out  = os.path.join(ATM_DIR, f'{name}.xml')
        with open(out, 'w', encoding='utf-8') as fh:
            fh.write(xml)
        risk = total * sl_ticks * TICK_VAL
        print(f'ATM {name}.xml  total={total}  T1={t1q}x{t1_tgt}T  T2={t2q}x{t2_tgt}T  SL={sl_ticks}T  risk=${risk:.0f}')
        written_atm += 1

print()

# ── Write DTDB ATM XMLs ───────────────────────────────────────────────────────
DTDB_SS = 'MNQ Scotty-SL20'
for series, sl_ticks, total, t1q, t2q, t1_tgt, t2_tgt in MNQ_DTDB:
    name = f'MNQ ${series} DTDB'
    xml  = build_atm_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, DTDB_SS)
    out  = os.path.join(ATM_DIR, f'{name}.xml')
    with open(out, 'w', encoding='utf-8') as fh:
        fh.write(xml)
    risk = total * sl_ticks * TICK_VAL
    print(f'DTDB {name}.xml  total={total}  T1={t1q}x{t1_tgt}T  T2={t2q}x{t2_tgt}T  SL={sl_ticks}T  risk=${risk:.0f}')
    written_atm += 1

print(f'\n✅ Stop Strategies: {written_ss}   ATMs: {written_atm}   Total files: {written_ss + written_atm}')
