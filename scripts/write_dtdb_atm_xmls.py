"""
write_dtdb_atm_xmls.py
Generates MES and MGC DTDB (Double Top / Double Bottom) ATM XML files
for the $100, $200, $300, and $400 series.

DTDB = SL4 baseline with a tighter stop:
  MES: stop = 8 ticks (2pt)   instead of 16 ticks (4pt)
  MGC: stop = 20 ticks (2pt)  instead of 40 ticks (4pt)
Everything else (quantities, targets, Scotty SL4 stop strategy) is identical to SL4.
"""
import os, sys
sys.stdout.reconfigure(encoding='utf-8')

ATM_DIR = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

# ── MES DTDB data ─────────────────────────────────────────────────────────────
# (series_label, sl_ticks, total, t1q, t2q, t1_tgt, t2_tgt)
MES_DTDB = [
    ('$100', 8, 5,  3,  2,  8, 12),
    ('$200', 8, 10, 5,  5,  8, 12),
    ('$300', 8, 14, 7,  7,  8, 12),
    ('$400', 8, 20, 10, 10, 8, 12),
]

# ── MGC DTDB data ─────────────────────────────────────────────────────────────
# (series_label, sl_ticks, total, t1q, t2q, t1_tgt, t2_tgt)
MGC_DTDB = [
    ('$100', 20, 4,  2, 2,  20, 30),
    ('$200', 20, 5,  3, 2,  20, 30),
    ('$300', 20, 7,  4, 3,  20, 30),
    ('$400', 20, 10, 5, 5,  20, 30),
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

MES_STOP_BLOCK = '''          <StopStrategy>
            <AutoBreakEvenPlus>2</AutoBreakEvenPlus>
            <AutoBreakEvenProfitTrigger>8</AutoBreakEvenProfitTrigger>
            <AutoTrailSteps>
              <AutoTrailStep><Frequency>4</Frequency><ProfitTrigger>8</ProfitTrigger><StopLoss>12</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>13</ProfitTrigger><StopLoss>8</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>16</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>
            </AutoTrailSteps>
            <IsSimStopEnabled>true</IsSimStopEnabled>
            <VolumeTrigger>1</VolumeTrigger>
            <Template>Scotty-SL4</Template>
          </StopStrategy>'''

MGC_STOP_BLOCK = '''          <StopStrategy>
            <AutoBreakEvenPlus>3</AutoBreakEvenPlus>
            <AutoBreakEvenProfitTrigger>20</AutoBreakEvenProfitTrigger>
            <AutoTrailSteps>
              <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
              <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>
            </AutoTrailSteps>
            <IsSimStopEnabled>true</IsSimStopEnabled>
            <VolumeTrigger>1</VolumeTrigger>
            <Template>MGC Scotty SL4</Template>
          </StopStrategy>'''


def build_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, stop_block):
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
{stop_block}
        <Target>{t1_tgt}</Target>
      </Bracket>
      <Bracket>
        <Quantity>{t2q}</Quantity>
        <StopLoss>{sl_ticks}</StopLoss>
{stop_block}
        <Target>{t2_tgt}</Target>
      </Bracket>
    </Brackets>
    <EntryQuantity>{total}</EntryQuantity>
{XML_FOOTER}'''


written = 0

# ── Write MES DTDB XMLs ───────────────────────────────────────────────────────
for series, sl_ticks, total, t1q, t2q, t1_tgt, t2_tgt in MES_DTDB:
    name = f'MES {series} DTDB'
    xml = build_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, MES_STOP_BLOCK)
    out = os.path.join(ATM_DIR, f'{name}.xml')
    with open(out, 'w', encoding='utf-8') as fh:
        fh.write(xml)
    risk = total * sl_ticks * 1.25
    print(f'OK  {name}.xml  total={total}  T1={t1q}x{t1_tgt}T  T2={t2q}x{t2_tgt}T  SL={sl_ticks}T  risk=${risk:.0f}')
    written += 1

# ── Write MGC DTDB XMLs ───────────────────────────────────────────────────────
for series, sl_ticks, total, t1q, t2q, t1_tgt, t2_tgt in MGC_DTDB:
    name = f'MGC {series} DTDB'
    xml = build_xml(name, total, t1q, t2q, sl_ticks, t1_tgt, t2_tgt, MGC_STOP_BLOCK)
    out = os.path.join(ATM_DIR, f'{name}.xml')
    with open(out, 'w', encoding='utf-8') as fh:
        fh.write(xml)
    risk = total * sl_ticks * 1.00
    print(f'OK  {name}.xml  total={total}  T1={t1q}x{t1_tgt}T  T2={t2q}x{t2_tgt}T  SL={sl_ticks}T  risk=${risk:.0f}')
    written += 1

print(f'\nTotal: {written} DTDB XML files written to NinjaTrader ATM folder')
