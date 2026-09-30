import sys, math, os
sys.stdout.reconfigure(encoding='utf-8')

NT_STOP = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\StopStrategy'
NT_ATM  = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

# ── Universal "MGC Scotty SOP" StopStrategy ───────────────────────────────
# Taken directly from Scotty's (Wall Street Global Trading Academy) GC setup:
#   BE trigger : 10 ticks -> stop moves to entry +2 ticks
#   Step 1     : profit >= 20 ticks -> stop = 5 ticks   (freq 2)
#   Step 2     : profit >= 30 ticks -> stop = 5 ticks   (freq 2)
#   Step 3     : profit >= 40 ticks -> stop = 2 ticks   (freq 1)
#   Simulated stop: volume trigger 1, enabled
#   Applied universally to ALL MGC SL4-SL10 (Q2 and Q3 brackets)

SOP_NAME = 'MGC Scotty SOP'

SOP_XML = '''<?xml version="1.0" encoding="utf-8"?>
<NinjaTrader>
  <StopStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <AutoBreakEvenPlus>2</AutoBreakEvenPlus>
    <AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>
    <AutoTrailSteps>
      <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
      <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
      <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>
    </AutoTrailSteps>
    <IsSimStopEnabled>true</IsSimStopEnabled>
    <VolumeTrigger>1</VolumeTrigger>
    <Template>MGC Scotty SOP</Template>
  </StopStrategy>
</NinjaTrader>'''

print('=== Writing MGC Scotty SOP StopStrategy ===')
fpath = os.path.join(NT_STOP, SOP_NAME + '.xml')
with open(fpath, 'w', encoding='utf-8') as f:
    f.write(SOP_XML)
print(f'  OK  {fpath}')
print('    BE=10tk +2tk | Step1: @20->5tk(x2) | Step2: @30->5tk(x2) | Step3: @40->2tk(x1) | SimStop ON vol=1')

# ── Rebuild all MGC ATM XMLs using the universal Scotty SOP ──────────────
ATM_XML = '''<?xml version="1.0" encoding="utf-8"?>
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
{brackets}    </Brackets>
    <EntryQuantity>{total}</EntryQuantity>
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
</NinjaTrader>'''

B1 = '''      <Bracket>
        <Quantity>{q}</Quantity>
        <StopLoss>{sl}</StopLoss>
        <Target>{t}</Target>
      </Bracket>
'''

# Scotty SOP bracket: BE=10tk+2, trails @20->5(x2), @30->5(x2), @40->2(x1), SimStop vol=1
BSOP = '''      <Bracket>
        <Quantity>{q}</Quantity>
        <StopLoss>{sl}</StopLoss>
        <StopStrategy>
          <AutoBreakEvenPlus>2</AutoBreakEvenPlus>
          <AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>
          <AutoTrailSteps>
            <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
            <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>
            <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>
          </AutoTrailSteps>
          <IsSimStopEnabled>true</IsSimStopEnabled>
          <VolumeTrigger>1</VolumeTrigger>
          <Template>MGC Scotty SOP</Template>
        </StopStrategy>
        <Target>{t}</Target>
      </Bracket>
'''

# MGC: 10 ticks/pt, $1/tick
# T1 = 10 ticks (1pt) — fixed for all SLs
# T2 = 50% of SL in ticks
# T3 = 75% of SL in ticks

QTY = {
    400: {4:10, 5:8, 6:6, 7:5, 8:5, 9:4, 10:4},
    200: {4:5,  5:4, 6:3, 7:3, 8:2, 9:2, 10:2},
}

RISK_LABELS = {400: '$400', 200: '$200'}

def make_atm(sl_pts, total, risk_label):
    sl_tk = sl_pts * 10
    t1_tk = 10                          # fixed 1pt for all SLs
    t2_tk = sl_tk // 2                  # 50% of SL
    t3_tk = int(sl_tk * 0.75)          # 75% of SL
    q1 = math.ceil(total / 2)
    q2 = math.ceil((total - q1) / 2)
    q3 = total - q1 - q2
    name = f'MGC {risk_label} SL{sl_pts}'

    b1 = B1.format(q=q1, sl=sl_tk, t=t1_tk)
    b2 = BSOP.format(q=q2, sl=sl_tk, t=t2_tk)
    brackets = b1 + b2
    if q3 > 0:
        b3 = BSOP.format(q=q3, sl=sl_tk, t=t3_tk)
        brackets += b3

    return ATM_XML.format(name=name, brackets=brackets, total=total), name, q1, q2, q3, sl_tk, t1_tk, t2_tk, t3_tk

print('\n=== Writing MGC ATM XMLs (all SL4-SL10, $400 and $200) ===')
for risk in [400, 200]:
    for sl in [4, 5, 6, 7, 8, 9, 10]:
        total = QTY[risk][sl]
        xml, name, q1, q2, q3, sl_tk, t1, t2, t3 = make_atm(sl, total, RISK_LABELS[risk])
        fpath = os.path.join(NT_ATM, name + '.xml')
        with open(fpath, 'w', encoding='utf-8') as f:
            f.write(xml)
        print(f'  OK  {name}  Q={q1}+{q2}+{q3}  SL={sl_tk}tk  T1={t1}tk  T2={t2}tk  T3={t3}tk  -> MGC Scotty SOP')

print('\nDone. All MGC ATMs now reference "MGC Scotty SOP".')
