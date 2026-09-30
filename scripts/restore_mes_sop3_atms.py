import sys, math, os
sys.stdout.reconfigure(encoding='utf-8')

NT_ATM = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

# MES: 4 ticks/pt, $1.25/tick, $5/pt
# SOP3 trail (MES ticks): BE=T1, +2tk buffer
# Step1: @12tk→stop 4tk  Step2: @16tk→stop 10tk  Step3: @20tk→stop 16tk
# SOP35: BE=14tk, @21→6, @28→12, @35→18 ... wait — use exact values from original SOP files

# MES SOP trail ticks by SL (T1 = SL_tk // 2, then trail triggers scale proportionally)
# SOP3  (SL4/5/6): T1=8/10/12  — triggers at T1*1.5, T1*2.0, T1*2.5; stops at T1*0.5, T1*1.25, T1*2.0
# This matches the SOP3.xml exactly for SL4 (T1=8): 12,4 / 16,10 / 20,16

SOP_LABEL = {4:'SOP3', 5:'SOP3', 6:'SOP3', 7:'SOP35', 8:'SOP4', 9:'SOP45', 10:'SOP5'}

# MES quantities
QTY = {
    400: {4:20, 5:16, 6:13, 7:11, 8:10, 9:8, 10:8},
    200: {4:10, 5:8,  6:7,  7:6,  8:5,  9:5, 10:4},
}

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

BSOP = '''      <Bracket>
        <Quantity>{q}</Quantity>
        <StopLoss>{sl}</StopLoss>
        <StopStrategy>
          <AutoBreakEvenPlus>2</AutoBreakEvenPlus>
          <AutoBreakEvenProfitTrigger>{be}</AutoBreakEvenProfitTrigger>
          <AutoTrailSteps>
            <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{pt1}</ProfitTrigger><StopLoss>{sl1}</StopLoss></AutoTrailStep>
            <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{pt2}</ProfitTrigger><StopLoss>{sl2}</StopLoss></AutoTrailStep>
            <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>{pt3}</ProfitTrigger><StopLoss>{sl3}</StopLoss></AutoTrailStep>
          </AutoTrailSteps>
          <IsSimStopEnabled>false</IsSimStopEnabled>
          <VolumeTrigger>0</VolumeTrigger>
          <Template>{sop}</Template>
        </StopStrategy>
        <Target>{t}</Target>
      </Bracket>
'''

def make_mes_atm(sl_pts, total, risk_label):
    sl_tk  = sl_pts * 4           # MES: 4 ticks/pt
    t1_tk  = sl_tk // 2
    t2_tk  = int(sl_tk * 0.75)
    t3_tk  = sl_tk
    q1 = math.ceil(total / 2)
    q2 = math.ceil((total - q1) / 2)
    q3 = total - q1 - q2
    sop = SOP_LABEL[sl_pts]
    # Trail triggers: T1*1.5, T1*2.0, T1*2.5 / stops: T1*0.5, T1*1.25, T1*2.0
    pt1 = int(t1_tk * 1.5);  sl1 = int(t1_tk * 0.5)
    pt2 = int(t1_tk * 2.0);  sl2 = int(t1_tk * 1.25)
    pt3 = int(t1_tk * 2.5);  sl3 = int(t1_tk * 2.0)
    name = f'MES {risk_label} SL{sl_pts}'

    b1 = B1.format(q=q1, sl=sl_tk, t=t1_tk)
    b2 = BSOP.format(q=q2, sl=sl_tk, be=t1_tk,
                     pt1=pt1, sl1=sl1, pt2=pt2, sl2=sl2, pt3=pt3, sl3=sl3,
                     sop=sop, t=t2_tk)
    brackets = b1 + b2
    if q3 > 0:
        b3 = BSOP.format(q=q3, sl=sl_tk, be=t1_tk,
                         pt1=pt1, sl1=sl1, pt2=pt2, sl2=sl2, pt3=pt3, sl3=sl3,
                         sop=sop, t=t3_tk)
        brackets += b3
    return ATM_XML.format(name=name, brackets=brackets, total=total)

# Only rewrite the broken ones: SL4/5/6 for both $400 and $200
broken = [(400, 4), (400, 5), (400, 6), (200, 4), (200, 5), (200, 6)]

print('=== Restoring broken MES ATM XMLs ===')
for risk_level, sl_pts in broken:
    total = QTY[risk_level][sl_pts]
    risk_label = f'${risk_level}'
    xml = make_mes_atm(sl_pts, total, risk_label)
    fname = f'MES {risk_label} SL{sl_pts}.xml'
    fpath = os.path.join(NT_ATM, fname)
    with open(fpath, 'w', encoding='utf-8') as f:
        f.write(xml)
    sl_tk = sl_pts * 4; t1 = sl_tk // 2; t2 = int(sl_tk * 0.75)
    q1 = math.ceil(total/2); q2 = math.ceil((total-q1)/2); q3 = total-q1-q2
    print(f'  ✅ {fname:<25}  total={total}  Q={q1}+{q2}+{q3}  SL={sl_tk}tk  T1={t1}  T2={t2}  T3={sl_tk}  {SOP_LABEL[sl_pts]}')
