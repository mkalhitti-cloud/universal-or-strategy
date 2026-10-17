"""
write_300_atm_xmls.py
Generates MES $300 SL4-SL10 and MGC $300 SL4-SL10 NinjaTrader ATM XML files.
$300 series = $200 series + 4 extra contracts (MES) or +2 extra contracts (MGC).
MES: +2 on T1, +2 on T2. MGC: +1 on T1, +1 on T2.
All tick targets, stops, and Scotty stop parameters are IDENTICAL to the $200 series.
"""
import os, sys
sys.stdout.reconfigure(encoding='utf-8')

ATM_DIR = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

# ── MES $300 data ────────────────────────────────────────────────────────────
# Format: (sl, slT, total, t1q, t2q, t1tgt, t2tgt, scotty_template,
#          be_plus, be_trigger, s1_pt, s1_sl, s2_pt, s2_sl, s3_pt, s3_sl,
#          s1_freq, s2_freq, s3_freq)
MES_300 = [
    # sl  slT  tot t1  t2  T1T T2T  template          bep beTrig  s1pt s1sl s2pt s2sl s3pt s3sl f1 f2 f3
    (4,  16,  14,  7,  7,   8, 12, 'Scotty-SL4',      1,  8,   8,  12,  13,   8, 16,   2,  4,  2,  1),
    (5,  20,  12,  6,  6,   8, 12, 'Scotty-SL5',      1,  8,  10,  12,  17,   8, 20,   2,  4,  2,  1),
    (6,  24,  11,  6,  5,   8, 12, 'Scotty-SL6',      1,  8,  12,  12,  20,   8, 24,   2,  4,  2,  1),
    (7,  28,  10,  5,  5,   8, 14, 'Scotty-SL7',      1,  8,  14,  12,  23,   8, 28,   2,  4,  2,  1),
    (8,  32,   9,  5,  4,   8, 16, 'Scotty-SL8',      1,  8,  16,  12,  27,   8, 32,   2,  4,  2,  1),
    (9,  36,   9,  5,  4,   8, 18, 'Scotty-SL9',      1,  8,  18,  12,  30,   8, 36,   2,  4,  2,  1),
    (10, 40,   8,  4,  4,   8, 20, 'Scotty-SL10',     1,  8,  20,  12,  33,   8, 40,   2,  4,  2,  1),
]

# ── MGC $300 data ────────────────────────────────────────────────────────────
# MGC: bep=2, beTrig=20, trail steps identical across all SLs
MGC_300 = [
    # sl  slT  tot t1  t2  T1T T2T  template
    (4,  40,   7,  4,  3,  20, 30, 'MGC Scotty SL4'),
    (5,  50,   6,  3,  3,  20, 30, 'MGC Scotty SL5'),   # T2 target unchanged (30T from $200)
    (6,  60,   7,  4,  3,  20, 30, 'MGC Scotty SL6'),
    (7,  70,   6,  3,  3,  20, 35, 'MGC Scotty SL7'),
    (8,  80,   6,  3,  3,  20, 40, 'MGC Scotty SL8'),
    (9,  90,   6,  3,  3,  20, 45, 'MGC Scotty SL9'),
    (10,100,   6,  3,  3,  20, 50, 'MGC Scotty SL10'),
]

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


def mes_stop_strategy(bep, be_trigger, s1_pt, s1_sl, s2_pt, s2_sl, s3_pt, s3_sl, f1, f2, f3, template):
    return f'''          <StopStrategy>
            <AutoBreakEvenPlus>{bep}</AutoBreakEvenPlus>
            <AutoBreakEvenProfitTrigger>{be_trigger}</AutoBreakEvenProfitTrigger>
            <AutoTrailSteps>
              <AutoTrailStep><Frequency>{f1}</Frequency><ProfitTrigger>{s1_pt}</ProfitTrigger><StopLoss>{s1_sl}</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>{f2}</Frequency><ProfitTrigger>{s2_pt}</ProfitTrigger><StopLoss>{s2_sl}</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>{f3}</Frequency><ProfitTrigger>{s3_pt}</ProfitTrigger><StopLoss>{s3_sl}</StopLoss></AutoTrailStep>
            </AutoTrailSteps>
            <IsSimStopEnabled>true</IsSimStopEnabled>
            <VolumeTrigger>1</VolumeTrigger>
            <Template>{template}</Template>
          </StopStrategy>'''


def mgc_stop_strategy(template):
    return f'''          <StopStrategy>
            <AutoBreakEvenPlus>2</AutoBreakEvenPlus>
            <AutoBreakEvenProfitTrigger>20</AutoBreakEvenProfitTrigger>
            <AutoTrailSteps>
              <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>
            </AutoTrailSteps>
            <IsSimStopEnabled>true</IsSimStopEnabled>
            <VolumeTrigger>1</VolumeTrigger>
            <Template>{template}</Template>
          </StopStrategy>'''


def build_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, stop_block_fn, stop_block_fn_t2=None):
    if stop_block_fn_t2 is None:
        stop_block_fn_t2 = stop_block_fn
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
{stop_block_fn()}
        <Target>{t1_tgt}</Target>
      </Bracket>
      <Bracket>
        <Quantity>{t2q}</Quantity>
        <StopLoss>{sl_ticks}</StopLoss>
{stop_block_fn_t2()}
        <Target>{t2_tgt}</Target>
      </Bracket>
    </Brackets>
    <EntryQuantity>{total}</EntryQuantity>
{XML_FOOTER}'''


written = 0

# ── Write MES $300 XMLs ──────────────────────────────────────────────────────
for row in MES_300:
    sl, slT, total, t1q, t2q, t1tgt, t2tgt, tmpl, bep, betrg, s1pt, s1sl, s2pt, s2sl, s3pt, s3sl, f1, f2, f3 = row
    name = f'MES $300 SL{sl}'
    def make_stop(bep=bep, betrg=betrg, s1pt=s1pt, s1sl=s1sl, s2pt=s2pt, s2sl=s2sl,
                  s3pt=s3pt, s3sl=s3sl, f1=f1, f2=f2, f3=f3, tmpl=tmpl):
        return mes_stop_strategy(bep, betrg, s1pt, s1sl, s2pt, s2sl, s3pt, s3sl, f1, f2, f3, tmpl)
    xml = build_xml(name, total, t1q, t2q, slT, t1tgt, t2tgt, make_stop)
    out = os.path.join(ATM_DIR, f'{name}.xml')
    with open(out, 'w', encoding='utf-8') as fh:
        fh.write(xml)
    print(f'✅ {name}.xml  total={total}  T1={t1q}x{t1tgt}T  T2={t2q}x{t2tgt}T  SL={slT}T')
    written += 1

# ── Write MGC $300 XMLs ──────────────────────────────────────────────────────
for row in MGC_300:
    sl, slT, total, t1q, t2q, t1tgt, t2tgt, tmpl = row
    name = f'MGC $300 SL{sl}'
    def make_stop(tmpl=tmpl):
        return mgc_stop_strategy(tmpl)
    xml = build_xml(name, total, t1q, t2q, slT, t1tgt, t2tgt, make_stop)
    out = os.path.join(ATM_DIR, f'{name}.xml')
    with open(out, 'w', encoding='utf-8') as fh:
        fh.write(xml)
    print(f'✅ {name}.xml  total={total}  T1={t1q}x{t1tgt}T  T2={t2q}x{t2tgt}T  SL={slT}T')
    written += 1

print(f'\n✅ Total: {written} XML files written to NinjaTrader ATM folder')
